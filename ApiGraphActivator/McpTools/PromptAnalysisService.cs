using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Service that integrates prompt templates with AI analysis tools
/// </summary>
public class PromptAnalysisService
{
    private readonly PromptTemplateService _promptTemplateService;
    private readonly OpenAIService? _openAIService;
    private readonly ILogger<PromptAnalysisService> _logger;

    public PromptAnalysisService(
        PromptTemplateService promptTemplateService,
        ILogger<PromptAnalysisService> logger,
        OpenAIService? openAIService = null)
    {
        _promptTemplateService = promptTemplateService;
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a document using a specific prompt template
    /// </summary>
    public async Task<AnalysisResult> AnalyzeDocumentAsync(string templateName, DocumentAnalysisRequest request)
    {
        try
        {
            // Render the prompt template with the document data
            var promptParameters = CreatePromptParameters(request);
            var renderResult = await _promptTemplateService.RenderTemplateAsync(templateName, promptParameters);

            _logger.LogInformation("Rendered prompt template '{TemplateName}' for document analysis", templateName);

            // Get AI response using the rendered prompt (if OpenAI service is available)
            string aiResponse = "AI analysis not available - OpenAI service not configured";
            if (_openAIService != null)
            {
                try
                {
                    aiResponse = _openAIService.GetChatResponse(renderResult.RenderedPrompt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get OpenAI response, using default message");
                    aiResponse = $"AI analysis failed: {ex.Message}";
                }
            }

            var result = new AnalysisResult
            {
                TemplateName = templateName,
                TemplateDescription = renderResult.Template.Description,
                DocumentId = request.DocumentId,
                CompanyName = request.CompanyName,
                RenderedPrompt = renderResult.RenderedPrompt,
                AiResponse = aiResponse,
                Parameters = promptParameters,
                AnalysisDate = DateTime.UtcNow,
                Success = true
            };

            _logger.LogInformation("Completed document analysis using template '{TemplateName}' for company '{CompanyName}'", 
                templateName, request.CompanyName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document with template '{TemplateName}'", templateName);
            
            return new AnalysisResult
            {
                TemplateName = templateName,
                DocumentId = request.DocumentId,
                CompanyName = request.CompanyName,
                Success = false,
                ErrorMessage = ex.Message,
                AnalysisDate = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Get analysis suggestions based on document type and content
    /// </summary>
    public async Task<List<TemplateSuggestion>> GetAnalysisSuggestionsAsync(string documentType, string companyName)
    {
        var suggestions = new List<TemplateSuggestion>();

        try
        {
            var allTemplates = _promptTemplateService.GetAllTemplates();

            // Suggest relevant templates based on document type
            foreach (var template in allTemplates)
            {
                var relevanceScore = CalculateRelevanceScore(template, documentType);
                if (relevanceScore > 0)
                {
                    suggestions.Add(new TemplateSuggestion
                    {
                        TemplateName = template.Name,
                        Description = template.Description,
                        Category = template.Category,
                        RelevanceScore = relevanceScore,
                        Reason = GetSuggestionReason(template, documentType)
                    });
                }
            }

            suggestions = suggestions.OrderByDescending(s => s.RelevanceScore).ToList();

            _logger.LogDebug("Generated {SuggestionCount} template suggestions for document type '{DocumentType}'", 
                suggestions.Count, documentType);

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating analysis suggestions");
            return suggestions;
        }
    }

    /// <summary>
    /// Batch analyze multiple documents with the same template
    /// </summary>
    public async Task<List<AnalysisResult>> BatchAnalyzeAsync(string templateName, List<DocumentAnalysisRequest> requests)
    {
        var results = new List<AnalysisResult>();

        foreach (var request in requests)
        {
            try
            {
                var result = await AnalyzeDocumentAsync(templateName, request);
                results.Add(result);

                // Add small delay to respect rate limits
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch analysis for document '{DocumentId}'", request.DocumentId);
                
                results.Add(new AnalysisResult
                {
                    TemplateName = templateName,
                    DocumentId = request.DocumentId,
                    CompanyName = request.CompanyName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    AnalysisDate = DateTime.UtcNow
                });
            }
        }

        _logger.LogInformation("Completed batch analysis of {DocumentCount} documents using template '{TemplateName}'", 
            requests.Count, templateName);

        return results;
    }

    private Dictionary<string, object> CreatePromptParameters(DocumentAnalysisRequest request)
    {
        var parameters = new Dictionary<string, object>
        {
            ["companyName"] = request.CompanyName,
            ["documentType"] = request.DocumentType,
            ["documentContent"] = request.DocumentContent
        };

        if (request.FilingDate.HasValue)
        {
            parameters["filingDate"] = request.FilingDate.Value.ToString("yyyy-MM-dd");
        }

        if (request.ReportingPeriod != null)
        {
            parameters["reportingPeriod"] = request.ReportingPeriod;
        }

        // Add any custom parameters
        if (request.CustomParameters?.Any() == true)
        {
            foreach (var param in request.CustomParameters)
            {
                parameters[param.Key] = param.Value;
            }
        }

        return parameters;
    }

    private double CalculateRelevanceScore(PromptTemplate template, string documentType)
    {
        var score = 0.0;

        // Base relevance for all SEC filings
        if (template.Tags.Contains("sec-filing") || template.Tags.Contains("analysis"))
        {
            score += 0.3;
        }

        // Specific document type relevance
        switch (documentType.ToUpper())
        {
            case "10-K":
                if (template.Name.Contains("summary") || template.Name.Contains("risk") || template.Name.Contains("trend"))
                    score += 0.7;
                else if (template.Category == PromptCategories.DocumentAnalysis)
                    score += 0.5;
                break;

            case "10-Q":
                if (template.Name.Contains("financial") || template.Name.Contains("extraction") || template.Name.Contains("trend"))
                    score += 0.7;
                else if (template.Category == PromptCategories.FinancialExtraction)
                    score += 0.5;
                break;

            case "8-K":
                if (template.Name.Contains("summary") || template.Name.Contains("risk"))
                    score += 0.6;
                break;
        }

        // Category-based scoring
        if (template.Category == PromptCategories.DocumentAnalysis)
            score += 0.2;
        else if (template.Category == PromptCategories.FinancialExtraction)
            score += 0.2;

        return Math.Min(score, 1.0); // Cap at 1.0
    }

    private string GetSuggestionReason(PromptTemplate template, string documentType)
    {
        if (template.Name.Contains("summary"))
            return $"Ideal for generating comprehensive summaries of {documentType} filings";
        
        if (template.Name.Contains("financial"))
            return $"Perfect for extracting financial data from {documentType} documents";
        
        if (template.Name.Contains("risk"))
            return $"Specialized in identifying and assessing risks in {documentType} filings";
        
        if (template.Name.Contains("comparison"))
            return "Useful for comparing multiple companies or time periods";
        
        if (template.Name.Contains("trend"))
            return "Excellent for analyzing trends across multiple reporting periods";

        return $"General analysis template suitable for {documentType} documents";
    }
}

/// <summary>
/// Request model for document analysis
/// </summary>
public class DocumentAnalysisRequest
{
    public string DocumentId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentContent { get; set; } = string.Empty;
    public DateTime? FilingDate { get; set; }
    public string? ReportingPeriod { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Result of document analysis
/// </summary>
public class AnalysisResult
{
    public string TemplateName { get; set; } = string.Empty;
    public string? TemplateDescription { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? RenderedPrompt { get; set; }
    public string? AiResponse { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public DateTime AnalysisDate { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Template suggestion for document analysis
/// </summary>
public class TemplateSuggestion
{
    public string TemplateName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}