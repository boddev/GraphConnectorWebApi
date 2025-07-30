using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for financial data interpretation and insights
/// </summary>
public class FinancialAnalysisTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly OpenAIService _openAIService;
    private readonly ILogger<FinancialAnalysisTool> _logger;

    public FinancialAnalysisTool(
        DocumentSearchService searchService, 
        OpenAIService openAIService,
        ILogger<FinancialAnalysisTool> logger)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public override string Name => "analyze_financial_data";

    public override string Description => 
        "Perform comprehensive financial analysis of SEC filing documents, extracting key metrics, ratios, and providing business insights.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentIds = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document IDs to analyze (required)"
            },
            analysisType = new
            {
                type = "string",
                @enum = new[] { "comprehensive", "performance", "position", "ratios", "trends" },
                description = "Type of financial analysis to perform (default: comprehensive)"
            },
            includeProjections = new
            {
                type = "boolean",
                description = "Include forward-looking statements and projections (default: true)"
            },
            includeRatios = new
            {
                type = "boolean",
                description = "Include financial ratio analysis (default: true)"
            },
            timeFrame = new
            {
                type = "string",
                @enum = new[] { "current", "historical", "comparative" },
                description = "Time frame for analysis (default: current)"
            }
        },
        required = new[] { "documentIds" }
    };

    public async Task<McpToolResponse<AnalysisResultData>> ExecuteAsync(FinancialAnalysisParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting financial analysis for {DocumentCount} documents", parameters.DocumentIds.Count);

            // Validate parameters
            if (parameters.DocumentIds?.Any() != true)
            {
                return McpToolResponse<AnalysisResultData>.Error("Document IDs are required for financial analysis");
            }

            if (parameters.DocumentIds.Count > 6)
            {
                return McpToolResponse<AnalysisResultData>.Error("Maximum 6 documents can be analyzed at once");
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
                        Content = TruncateContent(document.FullContent ?? document.ContentPreview, 10000), // More content for financial analysis
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
                SystemPrompt = GetFinancialAnalysisPrompt(parameters),
                UserPrompt = BuildFinancialAnalysisPrompt(parameters, documentContexts),
                DocumentContexts = documentContexts,
                Temperature = 0.0f, // Very low temperature for financial analysis
                TopP = 0.7f
            };

            // Perform AI analysis
            var analysisResult = _openAIService.AnalyzeDocument(analysisRequest);

            // Parse and structure the results
            var structuredResult = ParseFinancialAnalysisResponse(analysisResult, documentContexts, parameters);

            _logger.LogInformation("Financial analysis completed. Tokens used: {TokensUsed}", analysisResult.TokensUsed);

            var metadata = new Dictionary<string, object>
            {
                { "analysisType", "financial_analysis" },
                { "financialAnalysisType", parameters.AnalysisType },
                { "timeFrame", parameters.TimeFrame },
                { "documentsAnalyzed", parameters.DocumentIds.Count },
                { "includeProjections", parameters.IncludeProjections },
                { "includeRatios", parameters.IncludeRatios },
                { "tokensUsed", analysisResult.TokensUsed },
                { "executionTime", DateTime.UtcNow }
            };

            return McpToolResponse<AnalysisResultData>.Success(structuredResult, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during financial analysis");
            return McpToolResponse<AnalysisResultData>.Error($"Financial analysis failed: {ex.Message}");
        }
    }

    private string GetFinancialAnalysisPrompt(FinancialAnalysisParameters parameters)
    {
        var basePrompt = OpenAIService.PromptTemplates.FinancialAnalysis;
        
        var customizations = new List<string>();
        
        if (parameters.AnalysisType != "comprehensive")
        {
            customizations.Add($"Focus specifically on {parameters.AnalysisType} analysis.");
        }
        
        if (!parameters.IncludeProjections)
        {
            customizations.Add("Do not include forward-looking statements or projections in the analysis.");
        }
        
        if (!parameters.IncludeRatios)
        {
            customizations.Add("Do not calculate or include financial ratios in the analysis.");
        }
        
        if (parameters.TimeFrame == "historical")
        {
            customizations.Add("Focus on historical trends and year-over-year changes.");
        }
        else if (parameters.TimeFrame == "comparative")
        {
            customizations.Add("Emphasize comparative analysis across the provided documents.");
        }
        
        if (customizations.Any())
        {
            return basePrompt + "\n\nAdditional Instructions:\n" + string.Join("\n", customizations);
        }
        
        return basePrompt;
    }

    private string BuildFinancialAnalysisPrompt(FinancialAnalysisParameters parameters, List<DocumentContext> documentContexts)
    {
        var promptParts = new List<string>
        {
            $"Please perform a {parameters.AnalysisType} financial analysis of the provided SEC filing documents."
        };

        if (parameters.IncludeRatios)
        {
            promptParts.Add("Calculate and interpret key financial ratios including liquidity, profitability, efficiency, and leverage ratios.");
        }

        if (parameters.IncludeProjections)
        {
            promptParts.Add("Include analysis of management guidance, projections, and forward-looking statements.");
        }

        switch (parameters.TimeFrame)
        {
            case "historical":
                promptParts.Add("Focus on historical performance trends and year-over-year changes.");
                break;
            case "comparative":
                promptParts.Add("Provide comparative analysis across all documents, highlighting differences and trends.");
                break;
            default:
                promptParts.Add("Analyze the current financial position and performance.");
                break;
        }

        promptParts.Add("Provide specific numerical data and calculations with document citations.");
        promptParts.Add("Highlight any unusual items, risk factors, or significant changes.");

        // Add document context
        promptParts.Add("\nDocuments for analysis:");
        for (int i = 0; i < documentContexts.Count; i++)
        {
            var doc = documentContexts[i];
            promptParts.Add($"Document {i + 1}: {doc.CompanyName} {doc.FormType} filed on {doc.FilingDate:yyyy-MM-dd}");
        }

        return string.Join(" ", promptParts);
    }

    private AnalysisResultData ParseFinancialAnalysisResponse(AnalysisResult analysisResult, List<DocumentContext> documentContexts, FinancialAnalysisParameters parameters)
    {
        var response = analysisResult.Response;
        
        // Extract financial metrics from the response
        var metrics = ExtractFinancialMetrics(response);
        
        // Extract key findings specific to financial analysis
        var keyFindings = ExtractFinancialKeyFindings(response);
        
        // Extract financial insights
        var insights = ExtractFinancialInsights(response, documentContexts, parameters);
        
        // Calculate confidence based on financial data coverage
        var confidence = CalculateFinancialAnalysisConfidence(analysisResult.Citations, response, metrics);

        return new AnalysisResultData
        {
            Summary = response,
            KeyFindings = keyFindings,
            Metrics = metrics,
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
            AnalysisType = $"financial_{parameters.AnalysisType}",
            TokenUsage = analysisResult.TokensUsed
        };
    }

    private Dictionary<string, object> ExtractFinancialMetrics(string response)
    {
        var metrics = new Dictionary<string, object>();
        
        // Extract various financial metrics using regex patterns
        var patterns = new Dictionary<string, string>
        {
            { "revenue", @"revenue[:\s]*\$?([\d,]+\.?\d*)\s*(?:million|billion|k|m|b)?" },
            { "net_income", @"net\s+income[:\s]*\$?([\d,]+\.?\d*)\s*(?:million|billion|k|m|b)?" },
            { "total_assets", @"total\s+assets[:\s]*\$?([\d,]+\.?\d*)\s*(?:million|billion|k|m|b)?" },
            { "total_debt", @"total\s+debt[:\s]*\$?([\d,]+\.?\d*)\s*(?:million|billion|k|m|b)?" },
            { "cash", @"cash[:\s]*\$?([\d,]+\.?\d*)\s*(?:million|billion|k|m|b)?" },
            { "operating_margin", @"operating\s+margin[:\s]*([\d,]+\.?\d*)%" },
            { "net_margin", @"net\s+margin[:\s]*([\d,]+\.?\d*)%" },
            { "debt_to_equity", @"debt[\/\s-]+to[\/\s-]+equity[:\s]*([\d,]+\.?\d*)" },
            { "current_ratio", @"current\s+ratio[:\s]*([\d,]+\.?\d*)" },
            { "roe", @"(?:return\s+on\s+equity|roe)[:\s]*([\d,]+\.?\d*)%" },
            { "roa", @"(?:return\s+on\s+assets|roa)[:\s]*([\d,]+\.?\d*)%" }
        };

        foreach (var pattern in patterns)
        {
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    response, pattern.Value, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                    System.Text.RegularExpressions.RegexOptions.Multiline);

                var values = new List<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        values.Add(match.Groups[1].Value);
                    }
                }

                if (values.Any())
                {
                    metrics[pattern.Key] = values;
                }
            }
            catch (Exception)
            {
                // Skip invalid regex patterns
            }
        }

        // Extract growth rates
        var growthMatches = System.Text.RegularExpressions.Regex.Matches(
            response, @"([\d,]+\.?\d*)%\s+(?:growth|increase|decrease)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var growthRates = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in growthMatches)
        {
            growthRates.Add(match.Groups[1].Value + "%");
        }

        if (growthRates.Any())
        {
            metrics["growth_rates"] = growthRates;
        }

        return metrics;
    }

    private List<string> ExtractFinancialKeyFindings(string response)
    {
        var findings = new List<string>();
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        // Financial keywords that indicate important findings
        var financialKeywords = new[]
        {
            "revenue", "profit", "loss", "margin", "ratio", "debt", "cash", "assets",
            "growth", "decline", "increase", "decrease", "performance", "efficiency"
        };
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length < 20) continue; // Skip very short sentences
            
            // Check for financial keywords and quantitative data
            var containsFinancialKeyword = financialKeywords.Any(kw => 
                trimmed.Contains(kw, StringComparison.OrdinalIgnoreCase));
            
            var containsNumbers = System.Text.RegularExpressions.Regex.IsMatch(
                trimmed, @"[\d,]+\.?\d*(?:%|\$|million|billion)");
            
            if (containsFinancialKeyword && containsNumbers)
            {
                findings.Add(trimmed + ".");
            }
        }

        return findings.Take(12).ToList(); // Top 12 financial findings
    }

    private List<AnalysisInsight> ExtractFinancialInsights(string response, List<DocumentContext> documentContexts, FinancialAnalysisParameters parameters)
    {
        var insights = new List<AnalysisInsight>();
        
        // Define financial insight categories
        var insightCategories = new Dictionary<string, string[]>
        {
            { "Profitability", new[] { "margin", "profit", "earnings", "profitability" } },
            { "Liquidity", new[] { "cash", "current ratio", "liquidity", "working capital" } },
            { "Leverage", new[] { "debt", "leverage", "debt-to-equity", "financial risk" } },
            { "Efficiency", new[] { "turnover", "efficiency", "utilization", "productivity" } },
            { "Growth", new[] { "growth", "expansion", "increase", "revenue growth" } },
            { "Performance", new[] { "performance", "returns", "roe", "roa" } }
        };

        foreach (var category in insightCategories)
        {
            var relevantSentences = ExtractSentencesForCategory(response, category.Value);
            
            foreach (var sentence in relevantSentences.Take(2)) // Top 2 insights per category
            {
                var supportingData = ExtractNumericalData(sentence);
                
                insights.Add(new AnalysisInsight
                {
                    Category = category.Key,
                    Insight = sentence,
                    Importance = DetermineFinancialImportance(sentence),
                    SupportingData = supportingData,
                    DocumentSources = documentContexts.Select(d => d.DocumentId).ToList()
                });
            }
        }

        // Add trend insights if analyzing multiple periods
        if (parameters.TimeFrame == "historical" || parameters.TimeFrame == "comparative")
        {
            var trendInsights = ExtractTrendInsights(response, documentContexts);
            insights.AddRange(trendInsights);
        }

        return insights.OrderByDescending(i => i.Importance == "high" ? 3 : i.Importance == "medium" ? 2 : 1).ToList();
    }

    private List<string> ExtractSentencesForCategory(string response, string[] keywords)
    {
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var relevantSentences = new List<string>();
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (keywords.Any(kw => trimmed.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                relevantSentences.Add(trimmed + ".");
            }
        }
        
        return relevantSentences;
    }

    private List<string> ExtractNumericalData(string sentence)
    {
        var data = new List<string>();
        
        // Extract various numerical patterns
        var patterns = new[]
        {
            @"\$[\d,]+\.?\d*(?:\s*(?:million|billion|k|m|b))?",  // Currency
            @"\d+\.?\d*%",                                        // Percentages
            @"\d+\.?\d*(?:\s*(?:ratio|times|x))",                // Ratios
            @"\d+\.?\d*(?:\s*(?:million|billion|thousand))"       // Large numbers
        };
        
        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(
                sentence, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                data.Add(match.Value);
            }
        }
        
        return data.Distinct().ToList();
    }

    private string DetermineFinancialImportance(string sentence)
    {
        var lowerSentence = sentence.ToLowerInvariant();
        
        // High importance indicators
        var highImportanceKeywords = new[]
        {
            "significant", "major", "substantial", "critical", "material", "dramatic",
            "strong", "excellent", "outstanding", "exceptional", "decline", "loss"
        };
        
        // Low importance indicators
        var lowImportanceKeywords = new[]
        {
            "minor", "slight", "small", "modest", "stable", "unchanged", "similar"
        };
        
        if (highImportanceKeywords.Any(kw => lowerSentence.Contains(kw)))
            return "high";
        if (lowImportanceKeywords.Any(kw => lowerSentence.Contains(kw)))
            return "low";
        
        // Check for numerical significance
        var hasLargeNumbers = System.Text.RegularExpressions.Regex.IsMatch(
            sentence, @"[\d,]*\d{3,}(?:\.?\d*)?(?:\s*(?:million|billion|%))", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return hasLargeNumbers ? "high" : "medium";
    }

    private List<AnalysisInsight> ExtractTrendInsights(string response, List<DocumentContext> documentContexts)
    {
        var insights = new List<AnalysisInsight>();
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        var trendKeywords = new[] { "trend", "increase", "decrease", "growth", "decline", "improved", "deteriorated" };
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trendKeywords.Any(kw => trimmed.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                var supportingData = ExtractNumericalData(trimmed);
                if (supportingData.Any())
                {
                    insights.Add(new AnalysisInsight
                    {
                        Category = "Trends",
                        Insight = trimmed + ".",
                        Importance = DetermineFinancialImportance(trimmed),
                        SupportingData = supportingData,
                        DocumentSources = documentContexts.Select(d => d.DocumentId).ToList()
                    });
                }
            }
        }
        
        return insights.Take(3).ToList(); // Top 3 trend insights
    }

    private double CalculateFinancialAnalysisConfidence(List<Services.DocumentCitation> citations, string response, Dictionary<string, object> metrics)
    {
        var confidence = 0.0;
        
        // Base confidence on citations
        if (citations.Any())
            confidence += 0.3;
        
        // Check for financial metrics extracted
        if (metrics.Any())
            confidence += Math.Min(0.4, metrics.Count * 0.05);
        
        // Check for quantitative analysis
        var numericalPatterns = System.Text.RegularExpressions.Regex.Matches(
            response, @"[\d,]+\.?\d*(?:%|\$|million|billion|ratio)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (numericalPatterns.Count > 5)
            confidence += 0.3;
        else if (numericalPatterns.Count > 2)
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