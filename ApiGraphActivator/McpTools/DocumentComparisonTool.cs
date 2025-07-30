using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for comparative analysis across multiple documents
/// </summary>
public class DocumentComparisonTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly OpenAIService _openAIService;
    private readonly ILogger<DocumentComparisonTool> _logger;

    public DocumentComparisonTool(
        DocumentSearchService searchService, 
        OpenAIService openAIService,
        ILogger<DocumentComparisonTool> logger)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public override string Name => "compare_documents";

    public override string Description => 
        "Perform comparative analysis across multiple SEC filing documents to identify differences, similarities, and trends.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentIds = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document IDs to compare (minimum 2, maximum 4)",
                minItems = 2,
                maxItems = 4
            },
            comparisonType = new
            {
                type = "string",
                @enum = new[] { "comprehensive", "financial", "operational", "strategic" },
                description = "Type of comparison to perform (default: comprehensive)"
            },
            focusAreas = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Specific areas to focus on in comparison (e.g., revenue, margins, risks, strategy)"
            }
        },
        required = new[] { "documentIds" }
    };

    public async Task<McpToolResponse<ComparisonResultData>> ExecuteAsync(DocumentComparisonParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting document comparison for {DocumentCount} documents", parameters.DocumentIds.Count);

            // Validate parameters
            if (parameters.DocumentIds?.Count < 2)
            {
                return McpToolResponse<ComparisonResultData>.Error("At least 2 documents are required for comparison");
            }

            if (parameters.DocumentIds.Count > 4)
            {
                return McpToolResponse<ComparisonResultData>.Error("Maximum 4 documents can be compared at once");
            }

            // Retrieve document contexts
            var documentContexts = new List<DocumentContext>();
            foreach (var documentId in parameters.DocumentIds)
            {
                var document = await _searchService.GetDocumentByIdAsync(documentId);
                if (document != null)
                {
                    documentContexts.Add(new DocumentContext
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        CompanyName = document.CompanyName,
                        FormType = document.FormType,
                        FilingDate = document.FilingDate,
                        Content = TruncateContent(document.FullContent ?? document.ContentPreview, 6000), // Balanced content length for comparison
                        Url = document.Url
                    });
                }
                else
                {
                    _logger.LogWarning("Document {DocumentId} not found", documentId);
                }
            }

            if (documentContexts.Count < 2)
            {
                return McpToolResponse<ComparisonResultData>.Error("At least 2 valid documents are required for comparison");
            }

            // Build analysis request
            var analysisRequest = new AnalysisRequest
            {
                SystemPrompt = OpenAIService.PromptTemplates.DocumentComparison,
                UserPrompt = BuildComparisonPrompt(parameters, documentContexts),
                DocumentContexts = documentContexts,
                Temperature = 0.1f,
                TopP = 0.9f
            };

            // Perform AI analysis
            var analysisResult = _openAIService.AnalyzeDocument(analysisRequest);

            // Parse and structure the results
            var structuredResult = ParseComparisonResponse(analysisResult, documentContexts, parameters);

            _logger.LogInformation("Document comparison completed. Tokens used: {TokensUsed}", analysisResult.TokensUsed);

            var metadata = new Dictionary<string, object>
            {
                { "analysisType", "document_comparison" },
                { "comparisonType", parameters.ComparisonType },
                { "documentsCompared", parameters.DocumentIds.Count },
                { "focusAreas", parameters.FocusAreas },
                { "tokensUsed", analysisResult.TokensUsed },
                { "executionTime", DateTime.UtcNow }
            };

            return McpToolResponse<ComparisonResultData>.Success(structuredResult, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document comparison");
            return McpToolResponse<ComparisonResultData>.Error($"Document comparison failed: {ex.Message}");
        }
    }

    private string BuildComparisonPrompt(DocumentComparisonParameters parameters, List<DocumentContext> documentContexts)
    {
        var promptParts = new List<string>
        {
            $"Please perform a {parameters.ComparisonType} comparison of the provided SEC filing documents."
        };

        if (parameters.FocusAreas?.Any() == true)
        {
            promptParts.Add($"Focus specifically on these areas: {string.Join(", ", parameters.FocusAreas)}.");
        }

        promptParts.Add("For each comparison, provide:");
        promptParts.Add("1. Key differences between the documents");
        promptParts.Add("2. Notable similarities or consistent patterns");
        promptParts.Add("3. Trends or changes over time (if applicable)");
        promptParts.Add("4. Specific data points and metrics with document citations");

        // Add document summary for context
        promptParts.Add("\nDocuments being compared:");
        for (int i = 0; i < documentContexts.Count; i++)
        {
            var doc = documentContexts[i];
            promptParts.Add($"Document {i + 1}: {doc.CompanyName} {doc.FormType} filed on {doc.FilingDate:yyyy-MM-dd}");
        }

        return string.Join(" ", promptParts);
    }

    private ComparisonResultData ParseComparisonResponse(AnalysisResult analysisResult, List<DocumentContext> documentContexts, DocumentComparisonParameters parameters)
    {
        var response = analysisResult.Response;
        
        // Extract key differences
        var keyDifferences = ExtractDifferences(response, documentContexts);
        
        // Extract similarities
        var similarities = ExtractSimilarities(response);
        
        // Extract trends
        var trends = ExtractTrends(response, documentContexts);
        
        // Create document summaries
        var documentSummaries = CreateDocumentSummaries(documentContexts, response);
        
        // Calculate confidence based on comparison depth
        var confidence = CalculateComparisonConfidence(analysisResult.Citations, response, documentContexts.Count);

        return new ComparisonResultData
        {
            Summary = response,
            KeyDifferences = keyDifferences,
            Similarities = similarities,
            Trends = trends,
            DocumentsAnalyzed = documentSummaries,
            Citations = analysisResult.Citations.Select(c => new DocumentCitation
            {
                DocumentId = c.DocumentId,
                DocumentTitle = c.DocumentTitle,
                CompanyName = c.CompanyName,
                FormType = c.FormType,
                FilingDate = c.FilingDate,
                Url = c.Url,
                RelevanceScore = c.RelevanceScore
            }).ToList(),
            Confidence = confidence
        };
    }

    private List<ComparisonDifference> ExtractDifferences(string response, List<DocumentContext> documentContexts)
    {
        var differences = new List<ComparisonDifference>();
        var sections = SplitIntoSections(response);
        
        foreach (var section in sections)
        {
            if (section.Contains("difference", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("change", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("versus", StringComparison.OrdinalIgnoreCase))
            {
                var difference = ParseDifferenceSection(section, documentContexts);
                if (difference != null)
                {
                    differences.Add(difference);
                }
            }
        }

        return differences.Take(10).ToList(); // Limit to top 10 differences
    }

    private List<string> ExtractSimilarities(string response)
    {
        var similarities = new List<string>();
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Contains("similar", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("consistent", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("same", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("both", StringComparison.OrdinalIgnoreCase))
            {
                similarities.Add(trimmed + ".");
            }
        }

        return similarities.Take(8).ToList(); // Top 8 similarities
    }

    private List<ComparisonTrend> ExtractTrends(string response, List<DocumentContext> documentContexts)
    {
        var trends = new List<ComparisonTrend>();
        var sections = SplitIntoSections(response);
        
        foreach (var section in sections)
        {
            if (section.Contains("trend", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("increase", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("decrease", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("growth", StringComparison.OrdinalIgnoreCase) ||
                section.Contains("decline", StringComparison.OrdinalIgnoreCase))
            {
                var trend = ParseTrendSection(section, documentContexts);
                if (trend != null)
                {
                    trends.Add(trend);
                }
            }
        }

        return trends.Take(6).ToList(); // Top 6 trends
    }

    private List<DocumentSummary> CreateDocumentSummaries(List<DocumentContext> documentContexts, string response)
    {
        var summaries = new List<DocumentSummary>();
        
        foreach (var doc in documentContexts)
        {
            var keyMetrics = ExtractDocumentMetrics(doc, response);
            
            summaries.Add(new DocumentSummary
            {
                DocumentId = doc.DocumentId,
                Title = doc.Title,
                CompanyName = doc.CompanyName,
                FormType = doc.FormType,
                FilingDate = doc.FilingDate,
                KeyMetrics = keyMetrics
            });
        }

        return summaries;
    }

    private ComparisonDifference? ParseDifferenceSection(string section, List<DocumentContext> documentContexts)
    {
        try
        {
            var category = DetermineDifferenceCategory(section);
            var significance = DetermineDifferenceSignificance(section);
            
            // Try to extract document references
            var docReferences = ExtractDocumentReferences(section, documentContexts);
            
            if (docReferences.Count >= 2)
            {
                return new ComparisonDifference
                {
                    Category = category,
                    Description = section.Trim(),
                    Significance = significance,
                    DocumentA = docReferences[0],
                    DocumentB = docReferences[1]
                };
            }
            
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ComparisonTrend? ParseTrendSection(string section, List<DocumentContext> documentContexts)
    {
        try
        {
            var trendType = DetermineTrendType(section);
            var direction = DetermineTrendDirection(section);
            var dataPoints = ExtractTrendDataPoints(section, documentContexts);
            
            return new ComparisonTrend
            {
                TrendType = trendType,
                Description = section.Trim(),
                Direction = direction,
                DataPoints = dataPoints
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Dictionary<string, object> ExtractDocumentMetrics(DocumentContext doc, string response)
    {
        var metrics = new Dictionary<string, object>();
        
        // Extract financial metrics mentioned for this document
        var docMentions = ExtractDocumentMentions(doc, response);
        
        foreach (var mention in docMentions)
        {
            var extractedMetrics = ExtractMetricsFromText(mention);
            foreach (var metric in extractedMetrics)
            {
                if (!metrics.ContainsKey(metric.Key))
                {
                    metrics[metric.Key] = metric.Value;
                }
            }
        }

        return metrics;
    }

    private List<string> SplitIntoSections(string text)
    {
        var sections = new List<string>();
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var currentSection = new List<string>();
        
        foreach (var sentence in sentences)
        {
            currentSection.Add(sentence.Trim());
            
            // End section at paragraph breaks or when we have 3-4 sentences
            if (currentSection.Count >= 3 || sentence.Contains('\n'))
            {
                sections.Add(string.Join(". ", currentSection) + ".");
                currentSection.Clear();
            }
        }
        
        if (currentSection.Any())
        {
            sections.Add(string.Join(". ", currentSection) + ".");
        }
        
        return sections;
    }

    private string DetermineDifferenceCategory(string text)
    {
        var lowerText = text.ToLowerInvariant();
        
        if (lowerText.Contains("revenue") || lowerText.Contains("income") || lowerText.Contains("profit"))
            return "Financial Performance";
        if (lowerText.Contains("risk") || lowerText.Contains("challenge"))
            return "Risk Factors";
        if (lowerText.Contains("strategy") || lowerText.Contains("plan"))
            return "Strategic Direction";
        if (lowerText.Contains("operation") || lowerText.Contains("business"))
            return "Operations";
        if (lowerText.Contains("market") || lowerText.Contains("competition"))
            return "Market Position";
        
        return "General";
    }

    private string DetermineDifferenceSignificance(string text)
    {
        var lowerText = text.ToLowerInvariant();
        
        if (lowerText.Contains("significant") || lowerText.Contains("major") || lowerText.Contains("substantial"))
            return "high";
        if (lowerText.Contains("minor") || lowerText.Contains("slight") || lowerText.Contains("small"))
            return "low";
        
        return "medium";
    }

    private string DetermineTrendType(string text)
    {
        var lowerText = text.ToLowerInvariant();
        
        if (lowerText.Contains("revenue") || lowerText.Contains("sales"))
            return "Revenue Trend";
        if (lowerText.Contains("profit") || lowerText.Contains("margin"))
            return "Profitability Trend";
        if (lowerText.Contains("growth") || lowerText.Contains("expansion"))
            return "Growth Trend";
        if (lowerText.Contains("cost") || lowerText.Contains("expense"))
            return "Cost Trend";
        
        return "General Trend";
    }

    private string DetermineTrendDirection(string text)
    {
        var lowerText = text.ToLowerInvariant();
        
        if (lowerText.Contains("increase") || lowerText.Contains("growth") || lowerText.Contains("improve"))
            return "increasing";
        if (lowerText.Contains("decrease") || lowerText.Contains("decline") || lowerText.Contains("drop"))
            return "decreasing";
        if (lowerText.Contains("stable") || lowerText.Contains("consistent"))
            return "stable";
        if (lowerText.Contains("volatile") || lowerText.Contains("fluctuat"))
            return "volatile";
        
        return "unclear";
    }

    private List<DocumentReference> ExtractDocumentReferences(string text, List<DocumentContext> documentContexts)
    {
        var references = new List<DocumentReference>();
        
        for (int i = 0; i < documentContexts.Count; i++)
        {
            var doc = documentContexts[i];
            var docRef = $"Document {i + 1}";
            
            if (text.Contains(doc.CompanyName, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(docRef, StringComparison.OrdinalIgnoreCase))
            {
                references.Add(new DocumentReference
                {
                    DocumentId = doc.DocumentId,
                    Title = doc.Title,
                    Value = ExtractValueForDocument(text, doc),
                    Context = text
                });
            }
        }
        
        return references;
    }

    private List<TrendDataPoint> ExtractTrendDataPoints(string text, List<DocumentContext> documentContexts)
    {
        var dataPoints = new List<TrendDataPoint>();
        
        foreach (var doc in documentContexts)
        {
            var value = ExtractValueForDocument(text, doc);
            if (!string.IsNullOrEmpty(value))
            {
                dataPoints.Add(new TrendDataPoint
                {
                    DocumentId = doc.DocumentId,
                    Date = doc.FilingDate,
                    Value = value,
                    Metric = DetermineTrendType(text)
                });
            }
        }
        
        return dataPoints.OrderBy(dp => dp.Date).ToList();
    }

    private List<string> ExtractDocumentMentions(DocumentContext doc, string text)
    {
        var mentions = new List<string>();
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var sentence in sentences)
        {
            if (sentence.Contains(doc.CompanyName, StringComparison.OrdinalIgnoreCase) ||
                sentence.Contains(doc.FormType, StringComparison.OrdinalIgnoreCase))
            {
                mentions.Add(sentence.Trim());
            }
        }
        
        return mentions;
    }

    private Dictionary<string, object> ExtractMetricsFromText(string text)
    {
        var metrics = new Dictionary<string, object>();
        
        // Extract currency amounts
        var currencyMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"\$[\d,]+\.?\d*[BMK]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Extract percentages
        var percentMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"\d+\.?\d*%", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        int currencyIndex = 0;
        foreach (System.Text.RegularExpressions.Match match in currencyMatches)
        {
            metrics[$"currency_{currencyIndex++}"] = match.Value;
        }
        
        int percentIndex = 0;
        foreach (System.Text.RegularExpressions.Match match in percentMatches)
        {
            metrics[$"percentage_{percentIndex++}"] = match.Value;
        }
        
        return metrics;
    }

    private string ExtractValueForDocument(string text, DocumentContext doc)
    {
        // Simple extraction of values associated with a document
        var docMentions = text.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Contains(doc.CompanyName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        foreach (var mention in docMentions)
        {
            var currencyMatch = System.Text.RegularExpressions.Regex.Match(
                mention, @"\$[\d,]+\.?\d*[BMK]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (currencyMatch.Success)
                return currencyMatch.Value;
            
            var percentMatch = System.Text.RegularExpressions.Regex.Match(
                mention, @"\d+\.?\d*%", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (percentMatch.Success)
                return percentMatch.Value;
        }
        
        return "";
    }

    private double CalculateComparisonConfidence(List<Services.DocumentCitation> citations, string response, int documentCount)
    {
        var confidence = 0.0;
        
        // Base confidence on citations coverage
        var citedDocuments = citations.Select(c => c.DocumentId).Distinct().Count();
        confidence += (double)citedDocuments / documentCount * 0.4;
        
        // Check for comparison keywords
        var comparisonKeywords = new[] { "compared", "versus", "difference", "similar", "contrast" };
        var keywordCount = comparisonKeywords.Count(kw => response.Contains(kw, StringComparison.OrdinalIgnoreCase));
        confidence += Math.Min(0.3, keywordCount * 0.1);
        
        // Check response length (more comprehensive = higher confidence)
        if (response.Length > 500)
            confidence += 0.3;
        else if (response.Length > 200)
            confidence += 0.2;
        
        return Math.Min(confidence, 1.0);
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content ?? "";
            
        return content.Substring(0, maxLength) + "...";
    }
}