using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for cross-document relationship analysis
/// </summary>
public class CrossDocumentRelationshipTool : McpToolBase
{
    private readonly OpenAIService _openAIService;
    private readonly ILogger<CrossDocumentRelationshipTool> _logger;
    private readonly HttpClient _httpClient;

    public CrossDocumentRelationshipTool(OpenAIService openAIService, ILogger<CrossDocumentRelationshipTool> logger, HttpClient httpClient)
    {
        _openAIService = openAIService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public override string Name => "analyze_cross_document_relationships";

    public override string Description => 
        "Analyze relationships between multiple documents including timeline analysis, consistency checking, and evolution tracking.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentUrls = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document URLs to analyze (PDF supported)"
            },
            documentContents = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document contents to analyze (alternative to documentUrls)"
            },
            companyName = new
            {
                type = "string",
                description = "Company name to focus the analysis on"
            },
            analysisType = new
            {
                type = "string",
                @enum = new[] { "timeline", "consistency", "evolution", "relationships" },
                description = "Type of cross-document analysis to perform"
            },
            focusAreas = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Specific areas to focus on (e.g., 'financial performance', 'strategic initiatives')"
            },
            maxPagesPerDoc = new
            {
                type = "integer",
                minimum = 1,
                maximum = 200,
                description = "Maximum number of pages to process per document"
            }
        }
    };

    public async Task<McpToolResponse<CrossDocumentRelationshipResult>> ExecuteAsync(CrossDocumentRelationshipParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting cross-document relationship analysis: {Type}", parameters.AnalysisType);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<CrossDocumentRelationshipResult>.Error($"Validation failed: {errors}");
            }

            var hasUrls = parameters.DocumentUrls?.Any() == true;
            var hasContents = parameters.DocumentContents?.Any() == true;

            if (!hasUrls && !hasContents)
            {
                return McpToolResponse<CrossDocumentRelationshipResult>.Error("Either documentUrls or documentContents must be provided");
            }

            if (hasUrls && hasContents)
            {
                return McpToolResponse<CrossDocumentRelationshipResult>.Error("Provide either documentUrls or documentContents, not both");
            }

            var documentTexts = new List<string>();
            var documentSources = new List<string>();

            // Extract text from documents
            if (hasUrls)
            {
                for (int i = 0; i < parameters.DocumentUrls!.Count; i++)
                {
                    var url = parameters.DocumentUrls[i];
                    try
                    {
                        if (PdfProcessingService.IsPdfUrl(url))
                        {
                            _logger.LogInformation("Processing PDF {Index} from URL: {Url}", i + 1, url);
                            var text = await PdfProcessingService.ExtractTextFromPdfUrlAsync(url, _httpClient, parameters.MaxPagesPerDoc);
                            documentTexts.Add(text);
                            documentSources.Add(url);
                        }
                        else
                        {
                            _logger.LogWarning("Skipping non-PDF URL: {Url}", url);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process document {Index}: {Url}", i + 1, url);
                    }
                }
            }
            else
            {
                documentTexts.AddRange(parameters.DocumentContents!);
                for (int i = 0; i < documentTexts.Count; i++)
                {
                    documentSources.Add($"Document {i + 1}");
                }
            }

            if (!documentTexts.Any())
            {
                return McpToolResponse<CrossDocumentRelationshipResult>.Error("No valid documents could be processed");
            }

            _logger.LogInformation("Processing {Count} documents for cross-document analysis", documentTexts.Count);

            // Perform analysis based on type
            var result = parameters.AnalysisType.ToLower() switch
            {
                "timeline" => PerformTimelineAnalysis(documentTexts, documentSources, parameters),
                "consistency" => PerformConsistencyAnalysis(documentTexts, documentSources, parameters),
                "evolution" => PerformEvolutionAnalysis(documentTexts, documentSources, parameters),
                "relationships" => PerformRelationshipAnalysis(documentTexts, documentSources, parameters),
                _ => PerformComprehensiveAnalysis(documentTexts, documentSources, parameters)
            };

            result.DocumentCount = documentTexts.Count;
            result.AnalysisType = parameters.AnalysisType;

            _logger.LogInformation("Cross-document relationship analysis completed successfully");
            return McpToolResponse<CrossDocumentRelationshipResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cross-document relationship analysis");
            return McpToolResponse<CrossDocumentRelationshipResult>.Error($"Analysis failed: {ex.Message}");
        }
    }

    private CrossDocumentRelationshipResult PerformTimelineAnalysis(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var prompt = BuildTimelineAnalysisPrompt(documentTexts, documentSources, parameters);
        var aiResponse = _openAIService.GetChatResponse(prompt);
        
        var result = new CrossDocumentRelationshipResult();
        result.Timeline = ParseTimelineEvents(aiResponse, documentSources);
        result.Relationships = ParseDocumentRelationships(aiResponse, documentSources);
        
        return result;
    }

    private CrossDocumentRelationshipResult PerformConsistencyAnalysis(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var prompt = BuildConsistencyAnalysisPrompt(documentTexts, documentSources, parameters);
        var aiResponse = _openAIService.GetChatResponse(prompt);
        
        var result = new CrossDocumentRelationshipResult();
        result.ConsistencyAnalysis = ParseConsistencyAnalysis(aiResponse);
        result.Relationships = ParseDocumentRelationships(aiResponse, documentSources);
        
        return result;
    }

    private CrossDocumentRelationshipResult PerformEvolutionAnalysis(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var prompt = BuildEvolutionAnalysisPrompt(documentTexts, documentSources, parameters);
        var aiResponse = _openAIService.GetChatResponse(prompt);
        
        var result = new CrossDocumentRelationshipResult();
        result.EvolutionAnalysis = ParseEvolutionAnalysis(aiResponse);
        result.Relationships = ParseDocumentRelationships(aiResponse, documentSources);
        
        return result;
    }

    private CrossDocumentRelationshipResult PerformRelationshipAnalysis(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var prompt = BuildRelationshipAnalysisPrompt(documentTexts, documentSources, parameters);
        var aiResponse = _openAIService.GetChatResponse(prompt);
        
        var result = new CrossDocumentRelationshipResult();
        result.Relationships = ParseDocumentRelationships(aiResponse, documentSources);
        
        return result;
    }

    private CrossDocumentRelationshipResult PerformComprehensiveAnalysis(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var prompt = BuildComprehensiveAnalysisPrompt(documentTexts, documentSources, parameters);
        var aiResponse = _openAIService.GetChatResponse(prompt);
        
        var result = new CrossDocumentRelationshipResult();
        result.Timeline = ParseTimelineEvents(aiResponse, documentSources);
        result.ConsistencyAnalysis = ParseConsistencyAnalysis(aiResponse);
        result.EvolutionAnalysis = ParseEvolutionAnalysis(aiResponse);
        result.Relationships = ParseDocumentRelationships(aiResponse, documentSources);
        
        return result;
    }

    private string BuildTimelineAnalysisPrompt(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var companyContext = !string.IsNullOrEmpty(parameters.CompanyName) 
            ? $"Focus on events related to {parameters.CompanyName}." 
            : "";

        var focusContext = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var documentsText = BuildDocumentsText(documentTexts, documentSources);

        return $"""
Please analyze the following documents to create a chronological timeline of events and identify relationships between documents. {companyContext} {focusContext}

Format your response as follows:

TIMELINE_EVENTS:
- [YYYY-MM-DD] [Event description] - [Document source] - [Importance: high/medium/low]
- [YYYY-MM-DD] [Event description] - [Document source] - [Importance: high/medium/low]

DOCUMENT_RELATIONSHIPS:
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]

{documentsText}
""";
    }

    private string BuildConsistencyAnalysisPrompt(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var companyContext = !string.IsNullOrEmpty(parameters.CompanyName) 
            ? $"Focus on consistency in information about {parameters.CompanyName}." 
            : "";

        var focusContext = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var documentsText = BuildDocumentsText(documentTexts, documentSources);

        return $"""
Please analyze the following documents for consistency in information, identifying any inconsistencies or contradictions. {companyContext} {focusContext}

Format your response as follows:

CONSISTENCY_ANALYSIS:
Overall Consistency: [high/medium/low]

Inconsistencies:
- [Description of inconsistency]
- [Description of inconsistency]

Consistent Themes:
- [Description of consistent theme]
- [Description of consistent theme]

Analysis Notes: [Additional observations]

DOCUMENT_RELATIONSHIPS:
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]

{documentsText}
""";
    }

    private string BuildEvolutionAnalysisPrompt(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var companyContext = !string.IsNullOrEmpty(parameters.CompanyName) 
            ? $"Focus on the evolution of {parameters.CompanyName} over time." 
            : "";

        var focusContext = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var documentsText = BuildDocumentsText(documentTexts, documentSources);

        return $"""
Please analyze the following documents to track the evolution and changes over time. {companyContext} {focusContext}

Format your response as follows:

EVOLUTION_ANALYSIS:
Key Changes:
- [Description of key change]
- [Description of key change]

Evolution Trends:
- [Description of trend]
- [Description of trend]

Significant Developments:
- [Description of development]
- [Description of development]

Analysis Notes: [Additional observations about evolution]

DOCUMENT_RELATIONSHIPS:
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]

{documentsText}
""";
    }

    private string BuildRelationshipAnalysisPrompt(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var companyContext = !string.IsNullOrEmpty(parameters.CompanyName) 
            ? $"Focus on relationships relevant to {parameters.CompanyName}." 
            : "";

        var focusContext = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var documentsText = BuildDocumentsText(documentTexts, documentSources);

        return $"""
Please analyze the relationships between the following documents, identifying how they reference, complement, or contradict each other. {companyContext} {focusContext}

Format your response as follows:

DOCUMENT_RELATIONSHIPS:
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]

Relationship types can include: references, updates, contradicts, complements, supersedes, clarifies, etc.

{documentsText}
""";
    }

    private string BuildComprehensiveAnalysisPrompt(
        List<string> documentTexts, 
        List<string> documentSources, 
        CrossDocumentRelationshipParameters parameters)
    {
        var companyContext = !string.IsNullOrEmpty(parameters.CompanyName) 
            ? $"Focus the analysis on {parameters.CompanyName}." 
            : "";

        var focusContext = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var documentsText = BuildDocumentsText(documentTexts, documentSources);

        return $"""
Please perform a comprehensive cross-document analysis including timeline, consistency, evolution, and relationships. {companyContext} {focusContext}

Format your response as follows:

TIMELINE_EVENTS:
- [YYYY-MM-DD] [Event description] - [Document source] - [Importance: high/medium/low]

CONSISTENCY_ANALYSIS:
Overall Consistency: [high/medium/low]
Inconsistencies:
- [Description]
Consistent Themes:
- [Description]
Analysis Notes: [Additional observations]

EVOLUTION_ANALYSIS:
Key Changes:
- [Description]
Evolution Trends:
- [Description]
Significant Developments:
- [Description]
Analysis Notes: [Additional observations]

DOCUMENT_RELATIONSHIPS:
- [Source Doc] -> [Target Doc]: [Relationship type] - [Description] - [Confidence: 0.0-1.0]

{documentsText}
""";
    }

    private string BuildDocumentsText(List<string> documentTexts, List<string> documentSources)
    {
        var documentsBuilder = new System.Text.StringBuilder();
        documentsBuilder.AppendLine("DOCUMENTS TO ANALYZE:");
        
        for (int i = 0; i < documentTexts.Count; i++)
        {
            documentsBuilder.AppendLine($"");
            documentsBuilder.AppendLine($"--- DOCUMENT {i + 1}: {documentSources[i]} ---");
            
            // Limit document text to avoid token limits
            var text = documentTexts[i];
            if (text.Length > 15000) // Limit per document
            {
                text = text.Substring(0, 15000) + "... [truncated]";
            }
            
            documentsBuilder.AppendLine(text);
        }
        
        return documentsBuilder.ToString();
    }

    private List<TimelineEvent>? ParseTimelineEvents(string aiResponse, List<string> documentSources)
    {
        var timelineSection = ExtractSection(aiResponse, "TIMELINE_EVENTS");
        if (string.IsNullOrEmpty(timelineSection)) return null;

        var events = new List<TimelineEvent>();
        var eventMatches = Regex.Matches(timelineSection, @"- (\d{4}-\d{2}-\d{2})\s+(.*?)\s+-\s+(.*?)\s+-\s+Importance:\s*(high|medium|low)", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        foreach (Match match in eventMatches)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var eventDate))
            {
                events.Add(new TimelineEvent
                {
                    Date = eventDate,
                    Event = match.Groups[2].Value.Trim(),
                    SourceDocument = match.Groups[3].Value.Trim(),
                    Importance = match.Groups[4].Value.Trim().ToLower()
                });
            }
        }

        return events.Any() ? events : null;
    }

    private ConsistencyAnalysis? ParseConsistencyAnalysis(string aiResponse)
    {
        var consistencySection = ExtractSection(aiResponse, "CONSISTENCY_ANALYSIS");
        if (string.IsNullOrEmpty(consistencySection)) return null;

        var analysis = new ConsistencyAnalysis();

        // Parse overall consistency
        var overallMatch = Regex.Match(consistencySection, @"Overall Consistency:\s*(high|medium|low)", RegexOptions.IgnoreCase);
        if (overallMatch.Success)
        {
            analysis.OverallConsistency = overallMatch.Groups[1].Value.ToLower();
        }

        // Parse inconsistencies
        var inconsistenciesMatch = Regex.Match(consistencySection, @"Inconsistencies:(.*?)(?=Consistent Themes:|Analysis Notes:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (inconsistenciesMatch.Success)
        {
            var inconsistencies = ParseListItems(inconsistenciesMatch.Groups[1].Value);
            analysis.Inconsistencies = inconsistencies.Any() ? inconsistencies : null;
        }

        // Parse consistent themes
        var themesMatch = Regex.Match(consistencySection, @"Consistent Themes:(.*?)(?=Analysis Notes:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (themesMatch.Success)
        {
            var themes = ParseListItems(themesMatch.Groups[1].Value);
            analysis.ConsistentThemes = themes.Any() ? themes : null;
        }

        // Parse analysis notes
        var notesMatch = Regex.Match(consistencySection, @"Analysis Notes:\s*(.*?)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (notesMatch.Success)
        {
            analysis.AnalysisNotes = notesMatch.Groups[1].Value.Trim();
        }

        return analysis;
    }

    private EvolutionAnalysis? ParseEvolutionAnalysis(string aiResponse)
    {
        var evolutionSection = ExtractSection(aiResponse, "EVOLUTION_ANALYSIS");
        if (string.IsNullOrEmpty(evolutionSection)) return null;

        var analysis = new EvolutionAnalysis();

        // Parse key changes
        var changesMatch = Regex.Match(evolutionSection, @"Key Changes:(.*?)(?=Evolution Trends:|Significant Developments:|Analysis Notes:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (changesMatch.Success)
        {
            var changes = ParseListItems(changesMatch.Groups[1].Value);
            analysis.KeyChanges = changes.Any() ? changes : null;
        }

        // Parse evolution trends
        var trendsMatch = Regex.Match(evolutionSection, @"Evolution Trends:(.*?)(?=Significant Developments:|Analysis Notes:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (trendsMatch.Success)
        {
            var trends = ParseListItems(trendsMatch.Groups[1].Value);
            analysis.EvolutionTrends = trends.Any() ? trends : null;
        }

        // Parse significant developments
        var developmentsMatch = Regex.Match(evolutionSection, @"Significant Developments:(.*?)(?=Analysis Notes:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (developmentsMatch.Success)
        {
            var developments = ParseListItems(developmentsMatch.Groups[1].Value);
            analysis.SignificantDevelopments = developments.Any() ? developments : null;
        }

        // Parse analysis notes
        var notesMatch = Regex.Match(evolutionSection, @"Analysis Notes:\s*(.*?)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (notesMatch.Success)
        {
            analysis.AnalysisNotes = notesMatch.Groups[1].Value.Trim();
        }

        return analysis;
    }

    private List<DocumentRelationship> ParseDocumentRelationships(string aiResponse, List<string> documentSources)
    {
        var relationshipsSection = ExtractSection(aiResponse, "DOCUMENT_RELATIONSHIPS");
        if (string.IsNullOrEmpty(relationshipsSection)) return new List<DocumentRelationship>();

        var relationships = new List<DocumentRelationship>();
        var relationshipMatches = Regex.Matches(relationshipsSection, @"- (.*?)\s*->\s*(.*?):\s*(.*?)\s*-\s*(.*?)\s*-\s*Confidence:\s*([0-9]*\.?[0-9]+)", RegexOptions.Multiline);

        foreach (Match match in relationshipMatches)
        {
            if (double.TryParse(match.Groups[5].Value, out var confidence))
            {
                relationships.Add(new DocumentRelationship
                {
                    SourceDocument = match.Groups[1].Value.Trim(),
                    TargetDocument = match.Groups[2].Value.Trim(),
                    RelationshipType = match.Groups[3].Value.Trim(),
                    Description = match.Groups[4].Value.Trim(),
                    Confidence = confidence
                });
            }
        }

        return relationships;
    }

    private List<string> ParseListItems(string text)
    {
        var items = Regex.Matches(text, @"- (.*?)(?=\n|$)", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return items;
    }

    private string? ExtractSection(string text, string sectionName)
    {
        var pattern = $@"{sectionName}:\s*(.*?)(?=\n[A-Z_]+:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}