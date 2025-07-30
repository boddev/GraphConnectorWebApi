using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for document summarization using AI analysis
/// </summary>
public class DocumentSummarizationTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly OpenAIService _openAIService;
    private readonly ILogger<DocumentSummarizationTool> _logger;

    public DocumentSummarizationTool(
        DocumentSearchService searchService, 
        OpenAIService openAIService,
        ILogger<DocumentSummarizationTool> logger)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public override string Name => "summarize_documents";

    public override string Description => 
        "Generate comprehensive summaries of SEC filing documents using AI analysis. Supports different summary types and includes key metrics, business developments, and risk factors.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentIds = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document IDs to summarize (required)"
            },
            summaryType = new
            {
                type = "string",
                @enum = new[] { "comprehensive", "executive", "financial", "risks" },
                description = "Type of summary to generate (default: comprehensive)"
            },
            includeMetrics = new
            {
                type = "boolean",
                description = "Include key financial metrics in summary (default: true)"
            },
            includeRisks = new
            {
                type = "boolean",
                description = "Include risk factors in summary (default: true)"
            }
        },
        required = new[] { "documentIds" }
    };

    public async Task<McpToolResponse<AnalysisResultData>> ExecuteAsync(DocumentSummarizationParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting document summarization for {DocumentCount} documents", parameters.DocumentIds.Count);

            // Validate parameters
            if (parameters.DocumentIds?.Any() != true)
            {
                return McpToolResponse<AnalysisResultData>.Error("Document IDs are required for summarization");
            }

            if (parameters.DocumentIds.Count > 10)
            {
                return McpToolResponse<AnalysisResultData>.Error("Maximum 10 documents can be summarized at once");
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
                        Content = TruncateContent(document.FullContent ?? document.ContentPreview, 8000), // Limit content size
                        Url = document.Url
                    });
                }
                else
                {
                    _logger.LogWarning("Document {DocumentId} not found", documentId);
                }
            }

            if (!documentContexts.Any())
            {
                return McpToolResponse<AnalysisResultData>.Error("No valid documents found for the provided IDs");
            }

            // Build analysis request
            var analysisRequest = new AnalysisRequest
            {
                SystemPrompt = GetSummaryPrompt(parameters.SummaryType, parameters.IncludeMetrics, parameters.IncludeRisks),
                UserPrompt = BuildSummaryUserPrompt(parameters),
                DocumentContexts = documentContexts,
                Temperature = 0.1f,
                TopP = 0.9f
            };

            // Perform AI analysis
            var analysisResult = _openAIService.AnalyzeDocument(analysisRequest);

            // Parse and structure the results
            var structuredResult = ParseSummaryResponse(analysisResult, documentContexts, parameters.SummaryType);

            _logger.LogInformation("Document summarization completed. Tokens used: {TokensUsed}", analysisResult.TokensUsed);

            var metadata = new Dictionary<string, object>
            {
                { "analysisType", "summarization" },
                { "summaryType", parameters.SummaryType },
                { "documentsAnalyzed", parameters.DocumentIds.Count },
                { "tokensUsed", analysisResult.TokensUsed },
                { "executionTime", DateTime.UtcNow }
            };

            return McpToolResponse<AnalysisResultData>.Success(structuredResult, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document summarization");
            return McpToolResponse<AnalysisResultData>.Error($"Summarization failed: {ex.Message}");
        }
    }

    private string GetSummaryPrompt(string summaryType, bool includeMetrics, bool includeRisks)
    {
        var basePrompt = OpenAIService.PromptTemplates.DocumentSummarization;
        
        return summaryType.ToLowerInvariant() switch
        {
            "executive" => basePrompt + "\nFocus on executive-level insights and high-level strategic information. Keep the summary concise and focused on key decisions and outcomes.",
            "financial" => basePrompt + "\nFocus specifically on financial data, metrics, and performance indicators. Emphasize quantitative information and financial trends.",
            "risks" => basePrompt + "\nFocus primarily on risk factors, uncertainties, and potential challenges mentioned in the documents. Analyze both current and future risk exposures.",
            _ => basePrompt + (includeMetrics ? "\nInclude detailed financial metrics and performance data." : "") + 
                             (includeRisks ? "\nInclude comprehensive risk factor analysis." : "")
        };
    }

    private string BuildSummaryUserPrompt(DocumentSummarizationParameters parameters)
    {
        var promptParts = new List<string>
        {
            $"Please provide a {parameters.SummaryType} summary of the provided SEC filing documents."
        };

        if (parameters.IncludeMetrics)
        {
            promptParts.Add("Include key financial metrics and performance indicators.");
        }

        if (parameters.IncludeRisks)
        {
            promptParts.Add("Include important risk factors and uncertainties.");
        }

        promptParts.Add("Organize the summary with clear sections and provide specific citations for all information.");

        return string.Join(" ", promptParts);
    }

    private AnalysisResultData ParseSummaryResponse(AnalysisResult analysisResult, List<DocumentContext> documentContexts, string summaryType)
    {
        var response = analysisResult.Response;
        
        // Extract key findings from the response
        var keyFindings = ExtractKeyFindings(response);
        
        // Extract insights
        var insights = ExtractInsights(response, documentContexts);
        
        // Calculate confidence based on citation coverage
        var confidence = CalculateConfidence(analysisResult.Citations, documentContexts);

        return new AnalysisResultData
        {
            Summary = response,
            KeyFindings = keyFindings,
            Insights = insights,
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
            Confidence = confidence,
            AnalysisType = $"summarization_{summaryType}",
            TokenUsage = analysisResult.TokensUsed
        };
    }

    private List<string> ExtractKeyFindings(string response)
    {
        var findings = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Look for bullet points, numbered items, or lines starting with key indicators
            if (trimmed.StartsWith("•") || trimmed.StartsWith("-") || 
                trimmed.StartsWith("*") || char.IsDigit(trimmed.FirstOrDefault()) ||
                trimmed.StartsWith("Key") || trimmed.StartsWith("Important"))
            {
                findings.Add(trimmed.TrimStart('•', '-', '*', ' ').Trim());
            }
        }

        return findings.Take(10).ToList(); // Limit to top 10 findings
    }

    private List<AnalysisInsight> ExtractInsights(string response, List<DocumentContext> documentContexts)
    {
        var insights = new List<AnalysisInsight>();
        
        // Extract insights based on common financial keywords and patterns
        var insightKeywords = new Dictionary<string, string>
        {
            { "revenue", "Financial Performance" },
            { "profit", "Financial Performance" },
            { "growth", "Business Growth" },
            { "risk", "Risk Assessment" },
            { "market", "Market Analysis" },
            { "competition", "Competitive Position" },
            { "strategy", "Strategic Direction" },
            { "outlook", "Future Prospects" }
        };

        foreach (var keyword in insightKeywords)
        {
            var sentences = ExtractSentencesContaining(response, keyword.Key);
            foreach (var sentence in sentences.Take(2)) // Limit insights per category
            {
                insights.Add(new AnalysisInsight
                {
                    Category = keyword.Value,
                    Insight = sentence,
                    Importance = DetermineImportance(sentence),
                    DocumentSources = documentContexts.Select(d => d.DocumentId).ToList()
                });
            }
        }

        return insights;
    }

    private List<string> ExtractSentencesContaining(string text, string keyword)
    {
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return sentences
            .Where(s => s.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Trim() + ".")
            .ToList();
    }

    private string DetermineImportance(string sentence)
    {
        var highImportanceKeywords = new[] { "significant", "major", "critical", "substantial", "material" };
        var lowImportanceKeywords = new[] { "minor", "small", "limited", "moderate" };
        
        var lowerSentence = sentence.ToLowerInvariant();
        
        if (highImportanceKeywords.Any(k => lowerSentence.Contains(k)))
            return "high";
        if (lowImportanceKeywords.Any(k => lowerSentence.Contains(k)))
            return "low";
        
        return "medium";
    }

    private double CalculateConfidence(List<Services.DocumentCitation> citations, List<DocumentContext> documentContexts)
    {
        if (!documentContexts.Any()) return 0.0;
        
        var citedDocuments = citations.Select(c => c.DocumentId).Distinct().Count();
        var totalDocuments = documentContexts.Count;
        
        return (double)citedDocuments / totalDocuments;
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content ?? "";
            
        return content.Substring(0, maxLength) + "...";
    }
}