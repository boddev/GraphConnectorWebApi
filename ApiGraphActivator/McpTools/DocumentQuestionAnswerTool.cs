using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for document question answering using AI analysis
/// </summary>
public class DocumentQuestionAnswerTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly OpenAIService _openAIService;
    private readonly ILogger<DocumentQuestionAnswerTool> _logger;

    public DocumentQuestionAnswerTool(
        DocumentSearchService searchService, 
        OpenAIService openAIService,
        ILogger<DocumentQuestionAnswerTool> logger)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public override string Name => "answer_document_questions";

    public override string Description => 
        "Answer specific questions about SEC filing documents using AI analysis. Provides precise answers with document citations and supporting evidence.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            question = new
            {
                type = "string",
                description = "The question to answer based on the provided documents (required)"
            },
            documentIds = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of document IDs to search for the answer (required)"
            },
            includeContext = new
            {
                type = "boolean",
                description = "Include additional context around the answer (default: true)"
            },
            maxTokens = new
            {
                type = "integer",
                description = "Maximum tokens for the response (default: 4000)"
            }
        },
        required = new[] { "question", "documentIds" }
    };

    public async Task<McpToolResponse<AnalysisResultData>> ExecuteAsync(DocumentQuestionAnswerParameters parameters)
    {
        try
        {
            _logger.LogInformation("Processing question: {Question} for {DocumentCount} documents", 
                parameters.Question, parameters.DocumentIds.Count);

            // Validate parameters
            if (string.IsNullOrWhiteSpace(parameters.Question))
            {
                return McpToolResponse<AnalysisResultData>.Error("Question is required");
            }

            if (parameters.DocumentIds?.Any() != true)
            {
                return McpToolResponse<AnalysisResultData>.Error("Document IDs are required");
            }

            if (parameters.DocumentIds.Count > 5)
            {
                return McpToolResponse<AnalysisResultData>.Error("Maximum 5 documents can be analyzed for question answering");
            }

            // Retrieve document contexts
            var documentContexts = new List<DocumentContext>();
            foreach (var documentId in parameters.DocumentIds)
            {
                var document = await _searchService.GetDocumentByIdAsync(documentId);
                if (document != null)
                {
                    // For Q&A, we can include more content since we're focusing on a specific question
                    documentContexts.Add(new DocumentContext
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        CompanyName = document.CompanyName,
                        FormType = document.FormType,
                        FilingDate = document.FilingDate,
                        Content = TruncateContent(document.FullContent ?? document.ContentPreview, parameters.MaxTokens * 2), // Allow more content for Q&A
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
                SystemPrompt = OpenAIService.PromptTemplates.QuestionAnswering,
                UserPrompt = BuildQuestionPrompt(parameters),
                DocumentContexts = documentContexts,
                Temperature = 0.0f, // Lower temperature for factual Q&A
                TopP = 0.8f
            };

            // Perform AI analysis
            var analysisResult = _openAIService.AnalyzeDocument(analysisRequest);

            // Parse and structure the results
            var structuredResult = ParseQuestionAnswerResponse(analysisResult, documentContexts, parameters);

            _logger.LogInformation("Question answering completed. Tokens used: {TokensUsed}", analysisResult.TokensUsed);

            var metadata = new Dictionary<string, object>
            {
                { "analysisType", "question_answering" },
                { "question", parameters.Question },
                { "documentsAnalyzed", parameters.DocumentIds.Count },
                { "tokensUsed", analysisResult.TokensUsed },
                { "executionTime", DateTime.UtcNow }
            };

            return McpToolResponse<AnalysisResultData>.Success(structuredResult, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during question answering");
            return McpToolResponse<AnalysisResultData>.Error($"Question answering failed: {ex.Message}");
        }
    }

    private string BuildQuestionPrompt(DocumentQuestionAnswerParameters parameters)
    {
        var promptParts = new List<string>
        {
            $"Question: {parameters.Question}"
        };

        if (parameters.IncludeContext)
        {
            promptParts.Add("Please provide a comprehensive answer with relevant context and supporting information from the documents.");
        }
        else
        {
            promptParts.Add("Please provide a direct, concise answer to the question.");
        }

        promptParts.Add("Always cite the specific documents where you found the information.");
        promptParts.Add("If the answer cannot be found in the provided documents, clearly state that the information is not available.");

        return string.Join(" ", promptParts);
    }

    private AnalysisResultData ParseQuestionAnswerResponse(AnalysisResult analysisResult, List<DocumentContext> documentContexts, DocumentQuestionAnswerParameters parameters)
    {
        var response = analysisResult.Response;
        
        // Extract key findings from the response
        var keyFindings = ExtractAnswerKeyPoints(response, parameters.Question);
        
        // Extract insights related to the question
        var insights = ExtractQuestionInsights(response, documentContexts, parameters.Question);
        
        // Extract supporting evidence
        var supportingEvidence = ExtractSupportingEvidence(response);
        
        // Calculate confidence based on citation coverage and answer quality
        var confidence = CalculateAnswerConfidence(analysisResult.Citations, response, parameters.Question);

        return new AnalysisResultData
        {
            Summary = response,
            KeyFindings = keyFindings,
            Insights = insights,
            Metrics = new Dictionary<string, object>
            {
                { "questionLength", parameters.Question.Length },
                { "answerLength", response.Length },
                { "supportingEvidenceCount", supportingEvidence.Count },
                { "documentsReferenced", analysisResult.Citations.Count }
            },
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
            AnalysisType = "question_answering",
            TokenUsage = analysisResult.TokensUsed
        };
    }

    private List<string> ExtractAnswerKeyPoints(string response, string question)
    {
        var keyPoints = new List<string>();
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        // Look for sentences that directly address the question
        var questionKeywords = ExtractKeywords(question);
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length < 10) continue; // Skip very short sentences
            
            // Check if sentence contains question keywords or answer indicators
            if (questionKeywords.Any(keyword => trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                ContainsAnswerIndicators(trimmed))
            {
                keyPoints.Add(trimmed + ".");
            }
        }

        return keyPoints.Take(5).ToList(); // Top 5 key points
    }

    private List<AnalysisInsight> ExtractQuestionInsights(string response, List<DocumentContext> documentContexts, string question)
    {
        var insights = new List<AnalysisInsight>();
        
        // Categorize insights based on question type
        var questionType = CategorizeQuestion(question);
        
        var relevantSentences = ExtractRelevantSentences(response, question);
        
        foreach (var sentence in relevantSentences.Take(3)) // Top 3 insights
        {
            insights.Add(new AnalysisInsight
            {
                Category = questionType,
                Insight = sentence,
                Importance = DetermineAnswerImportance(sentence, question),
                SupportingData = ExtractDataFromSentence(sentence),
                DocumentSources = documentContexts.Select(d => d.DocumentId).ToList()
            });
        }

        return insights;
    }

    private List<string> ExtractSupportingEvidence(string response)
    {
        var evidence = new List<string>();
        var sentences = response.Split('.', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            // Look for sentences that contain specific data, numbers, or quotes
            if (ContainsEvidenceMarkers(trimmed))
            {
                evidence.Add(trimmed + ".");
            }
        }

        return evidence;
    }

    private double CalculateAnswerConfidence(List<Services.DocumentCitation> citations, string response, string question)
    {
        var confidence = 0.0;
        
        // Base confidence on citations
        if (citations.Any())
            confidence += 0.4;
        
        // Check if answer seems complete
        if (response.Length > 100 && !response.Contains("not available", StringComparison.OrdinalIgnoreCase))
            confidence += 0.3;
        
        // Check if answer directly addresses the question
        var questionKeywords = ExtractKeywords(question);
        var responseWords = response.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keywordMatches = questionKeywords.Count(kw => responseWords.Contains(kw.ToLowerInvariant()));
        
        if (keywordMatches > 0)
            confidence += Math.Min(0.3, keywordMatches * 0.1);
        
        return Math.Min(confidence, 1.0);
    }

    private List<string> ExtractKeywords(string text)
    {
        var keywords = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var cleanWord = word.Trim('?', '!', '.', ',', ';', ':').ToLowerInvariant();
            if (cleanWord.Length > 3 && !IsStopWord(cleanWord))
            {
                keywords.Add(cleanWord);
            }
        }
        
        return keywords.Distinct().ToList();
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string>
        {
            "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see", "two", "who", "boy", "did", "she", "use", "way", "what", "when", "where", "will", "with"
        };
        return stopWords.Contains(word);
    }

    private bool ContainsAnswerIndicators(string sentence)
    {
        var indicators = new[] { "according to", "states that", "indicates", "shows", "reveals", "reports", "demonstrates", "confirms" };
        return indicators.Any(indicator => sentence.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private string CategorizeQuestion(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();
        
        if (lowerQuestion.Contains("revenue") || lowerQuestion.Contains("profit") || lowerQuestion.Contains("income"))
            return "Financial Performance";
        if (lowerQuestion.Contains("risk") || lowerQuestion.Contains("challenge") || lowerQuestion.Contains("threat"))
            return "Risk Assessment";
        if (lowerQuestion.Contains("strategy") || lowerQuestion.Contains("plan") || lowerQuestion.Contains("future"))
            return "Strategic Direction";
        if (lowerQuestion.Contains("market") || lowerQuestion.Contains("competition") || lowerQuestion.Contains("industry"))
            return "Market Analysis";
        if (lowerQuestion.Contains("operation") || lowerQuestion.Contains("business") || lowerQuestion.Contains("management"))
            return "Operations";
        
        return "General Information";
    }

    private List<string> ExtractRelevantSentences(string text, string question)
    {
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var questionKeywords = ExtractKeywords(question);
        var relevantSentences = new List<(string sentence, int relevance)>();
        
        foreach (var sentence in sentences)
        {
            var relevance = 0;
            var sentenceWords = sentence.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var keyword in questionKeywords)
            {
                if (sentenceWords.Contains(keyword.ToLowerInvariant()))
                    relevance++;
            }
            
            if (relevance > 0)
            {
                relevantSentences.Add((sentence.Trim(), relevance));
            }
        }
        
        return relevantSentences
            .OrderByDescending(x => x.relevance)
            .Select(x => x.sentence)
            .ToList();
    }

    private string DetermineAnswerImportance(string sentence, string question)
    {
        var questionKeywords = ExtractKeywords(question);
        var sentenceWords = sentence.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keywordMatches = questionKeywords.Count(kw => sentenceWords.Contains(kw.ToLowerInvariant()));
        
        if (keywordMatches >= 2)
            return "high";
        if (keywordMatches == 1)
            return "medium";
        
        return "low";
    }

    private List<string> ExtractDataFromSentence(string sentence)
    {
        var data = new List<string>();
        
        // Simple regex patterns for common data types
        var patterns = new[]
        {
            @"\$[\d,]+\.?\d*[BMK]?", // Currency amounts
            @"\d+\.?\d*%", // Percentages
            @"\d{4}(?:\s*-\s*\d{4})?", // Years or year ranges
            @"\d+\.?\d*\s*(?:million|billion|thousand)", // Large numbers
        };
        
        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(sentence, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                data.Add(match.Value);
            }
        }
        
        return data;
    }

    private bool ContainsEvidenceMarkers(string sentence)
    {
        var markers = new[] { "$", "%", "million", "billion", "according", "stated", "reported", "disclosed" };
        return markers.Any(marker => sentence.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content ?? "";
            
        return content.Substring(0, maxLength) + "...";
    }
}