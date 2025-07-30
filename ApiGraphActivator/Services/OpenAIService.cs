using System;
using System.Collections.Generic;
using System.Text;
using OpenAI.Chat;
using Azure.Core;
using Azure.AI.OpenAI;
using Azure;

namespace ApiGraphActivator.Services;

public class OpenAIService
{
    private readonly Uri _endpoint = new Uri("https://bodopenai-eus.openai.azure.com/");
    private readonly string _model = "gpt-4o-mini";
    private readonly string _deploymentName = "gpt-4o-mini";
    private readonly string _apiKey = Environment.GetEnvironmentVariable("OpenAIKey");
    private readonly AzureOpenAIClient _azureClient;

    public OpenAIService()
    {
        _azureClient = new AzureOpenAIClient(
            _endpoint,
            new AzureKeyCredential(_apiKey));
    }

    public string GetChatResponse(string userInput)
    {
        var chatClient = _azureClient.GetChatClient(_deploymentName);

        ChatCompletionOptions requestOptions = new ChatCompletionOptions()
        {
            Temperature = 0.0f,
            TopP = 0.5f,
        };

        var messages = new List<ChatMessage>()
        {
            new SystemChatMessage("""
              You are a financial legal assistant.  Your job is to help the user with their financial legal questions.
              You are not a lawyer, and you cannot give legal advice.  You can only provide information and resources.
              The documents you will be working with are financial documents that are submitted to the SEC.  This documents 
              are 10-Q, 10-K, 8-K, Def14A and other SEC filings.  You will be working with the text of these documents.
              You will not be working with the images.Your main goal will be to extract all of the important information from the financial documents provided.  
              Be clear and concise in your responses.  You must keep the sentiment of the text.  You must not change the meaning of the text.
              You must not add any additional information to the text.  You must not remove any information from the text.
              If you do not know the answer to a question, say "I don't know" and do not try to make up an answer.
              Provide the answer in a paragraph format.  Do not use bullet points or lists.
              Do not use any special characters or formatting.  Do not use any HTML tags or markdown.
              Do not use any code blocks.  
              """
              ),
            new UserChatMessage(userInput)
        };

        ChatCompletion  response = chatClient.CompleteChat(messages, requestOptions);

        return response.Content[0].Text;
    }

    /// <summary>
    /// Analyze document with custom prompt and context injection
    /// </summary>
    public AnalysisResult AnalyzeDocument(AnalysisRequest request)
    {
        var chatClient = _azureClient.GetChatClient(_deploymentName);

        ChatCompletionOptions requestOptions = new ChatCompletionOptions()
        {
            Temperature = request.Temperature ?? 0.1f,
            TopP = request.TopP ?? 0.9f,
        };

        var messages = new List<ChatMessage>()
        {
            new SystemChatMessage(request.SystemPrompt),
            new UserChatMessage(BuildUserPrompt(request))
        };

        var response = chatClient.CompleteChat(messages, requestOptions);
        var responseText = response.Value.Content[0].Text;

        // Extract citations from the response if any document context was provided
        var citations = ExtractCitations(responseText, request.DocumentContexts);

        return new AnalysisResult
        {
            Response = responseText,
            Citations = citations,
            TokensUsed = 1000, // Default token count - will need to be updated with correct API
            Model = _model,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Stream analysis response for real-time output
    /// </summary>
    public async IAsyncEnumerable<AnalysisStreamChunk> AnalyzeDocumentStreamAsync(AnalysisRequest request)
    {
        // For now, implement non-streaming version until streaming API is fixed
        var result = AnalyzeDocument(request);
        
        yield return new AnalysisStreamChunk
        {
            Content = result.Response,
            IsComplete = true,
            Citations = result.Citations.Select(c => new DocumentCitation
            {
                DocumentId = c.DocumentId,
                DocumentTitle = c.DocumentTitle,
                CompanyName = c.CompanyName,
                FormType = c.FormType,
                FilingDate = c.FilingDate,
                Url = c.Url,
                RelevanceScore = c.RelevanceScore
            }).ToList(),
            TokensUsed = result.TokensUsed.ToString()
        };
    }

    private string BuildUserPrompt(AnalysisRequest request)
    {
        var promptBuilder = new StringBuilder();
        
        // Add the main user query
        promptBuilder.AppendLine(request.UserPrompt);
        
        // Add document context if provided
        if (request.DocumentContexts?.Any() == true)
        {
            promptBuilder.AppendLine("\n--- DOCUMENT CONTEXT ---");
            for (int i = 0; i < request.DocumentContexts.Count; i++)
            {
                var doc = request.DocumentContexts[i];
                promptBuilder.AppendLine($"\nDocument {i + 1}: {doc.Title}");
                promptBuilder.AppendLine($"Company: {doc.CompanyName}");
                promptBuilder.AppendLine($"Form Type: {doc.FormType}");
                promptBuilder.AppendLine($"Filing Date: {doc.FilingDate:yyyy-MM-dd}");
                promptBuilder.AppendLine($"Content: {doc.Content}");
                promptBuilder.AppendLine("---");
            }
        }

        return promptBuilder.ToString();
    }

    private List<DocumentCitation> ExtractCitations(string response, List<DocumentContext>? documentContexts)
    {
        var citations = new List<DocumentCitation>();
        
        if (documentContexts?.Any() != true)
            return citations;

        // Simple citation extraction - look for references to document titles or companies
        for (int i = 0; i < documentContexts.Count; i++)
        {
            var doc = documentContexts[i];
            var docRef = $"Document {i + 1}";
            
            if (response.Contains(doc.CompanyName, StringComparison.OrdinalIgnoreCase) ||
                response.Contains(doc.FormType, StringComparison.OrdinalIgnoreCase) ||
                response.Contains(docRef, StringComparison.OrdinalIgnoreCase))
            {
                citations.Add(new DocumentCitation
                {
                    DocumentId = doc.DocumentId,
                    DocumentTitle = doc.Title,
                    CompanyName = doc.CompanyName,
                    FormType = doc.FormType,
                    FilingDate = doc.FilingDate,
                    Url = doc.Url,
                    RelevanceScore = CalculateRelevanceScore(response, doc)
                });
            }
        }

        return citations.OrderByDescending(c => c.RelevanceScore).ToList();
    }

    private double CalculateRelevanceScore(string response, DocumentContext doc)
    {
        var score = 0.0;
        var responseWords = response.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var companyWords = doc.CompanyName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Calculate relevance based on word matches
        foreach (var word in companyWords)
        {
            if (responseWords.Contains(word))
                score += 0.3;
        }
        
        if (responseWords.Contains(doc.FormType.ToLowerInvariant()))
            score += 0.4;
            
        return Math.Min(score, 1.0);
    }

    /// <summary>
    /// Get appropriate prompt templates for different analysis types
    /// </summary>
    public static class PromptTemplates
    {
        public const string DocumentSummarization = """
            You are an expert financial document analyst. Your task is to provide comprehensive summaries of SEC filing documents.
            
            For each document, provide:
            1. Executive Summary (2-3 sentences capturing the key points)
            2. Key Financial Metrics (if present)
            3. Important Business Developments
            4. Risk Factors (if mentioned)
            5. Management Discussion Points
            
            Be factual and objective. Only include information explicitly stated in the document. Always include specific citations with document references.
            Format your response in clear sections. If multiple documents are provided, summarize each separately and then provide a comparative overview.
            """;

        public const string QuestionAnswering = """
            You are a knowledgeable financial document analyst. Answer the user's question based solely on the provided SEC filing documents.
            
            Guidelines:
            - Only use information explicitly stated in the documents
            - If the information is not available, clearly state "The information is not available in the provided documents"
            - Always cite specific documents when referencing information
            - Be precise and factual
            - Maintain the original meaning and context from the documents
            
            Provide a clear, direct answer to the question with supporting evidence from the documents.
            """;

        public const string DocumentComparison = """
            You are a financial analyst specializing in comparative document analysis. Compare the provided SEC filing documents and identify:
            
            1. Key Differences: Highlight significant changes between documents (financial metrics, business strategy, risks)
            2. Similarities: Identify consistent themes or stable aspects
            3. Trends: Note any patterns or progression across documents
            4. Notable Changes: Flag any unusual or significant variations
            
            Be objective and data-driven. Only compare information that is explicitly stated in the documents.
            Organize your analysis clearly with specific document citations for each point.
            """;

        public const string FinancialAnalysis = """
            You are a financial expert analyzing SEC filing documents. Focus on extracting and interpreting financial data and business insights:
            
            1. Financial Performance: Revenue, profit margins, growth rates, key financial ratios
            2. Financial Position: Assets, liabilities, cash flow, debt levels
            3. Business Operations: Operational efficiency, market position, competitive advantages
            4. Forward-Looking Statements: Management guidance, projections, strategic initiatives
            5. Risk Assessment: Financial risks, operational risks, market risks
            
            Provide quantitative analysis where possible with specific numbers and percentages from the documents.
            Be precise about what the data shows and avoid speculation beyond what's stated in the filings.
            Always cite the specific document and section where financial information was found.
            """;
    }
}

/// <summary>
/// Request model for document analysis
/// </summary>
public class AnalysisRequest
{
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public List<DocumentContext> DocumentContexts { get; set; } = new();
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
}

/// <summary>
/// Document context for analysis
/// </summary>
public class DocumentContext
{
    public string DocumentId { get; set; } = "";
    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string FormType { get; set; } = "";
    public DateTime FilingDate { get; set; }
    public string Content { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>
/// Analysis result with citations
/// </summary>
public class AnalysisResult
{
    public string Response { get; set; } = "";
    public List<DocumentCitation> Citations { get; set; } = new();
    public int TokensUsed { get; set; }
    public string Model { get; set; } = "";
    public string RequestId { get; set; } = "";
}

/// <summary>
/// Document citation information
/// </summary>
public class DocumentCitation
{
    public string DocumentId { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string FormType { get; set; } = "";
    public DateTime FilingDate { get; set; }
    public string Url { get; set; } = "";
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Streaming chunk for real-time analysis
/// </summary>
public class AnalysisStreamChunk
{
    public string Content { get; set; } = "";
    public bool IsComplete { get; set; }
    public List<DocumentCitation> Citations { get; set; } = new();
    public string? TokensUsed { get; set; }
}
