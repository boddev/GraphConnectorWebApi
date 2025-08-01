using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class CopilotChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://graph.microsoft.com/beta";
    private readonly ILogger<CopilotChatService> _logger;
    private readonly string? _accessToken;

    public CopilotChatService(ILogger<CopilotChatService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _accessToken = Environment.GetEnvironmentVariable("M365_ACCESS_TOKEN");
        
        if (string.IsNullOrEmpty(_accessToken))
        {
            _logger.LogWarning("M365_ACCESS_TOKEN environment variable not set. Chat functionality will be limited.");
        }
    }

    public async Task<string> GetChatResponseAsync(string userInput, string? conversationId = null, List<string>? additionalContext = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("M365 access token is not configured. Please set the M365_ACCESS_TOKEN environment variable.");
            }

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

            // Create new conversation if none provided
            if (string.IsNullOrEmpty(conversationId))
            {
                conversationId = await CreateConversationAsync();
            }

            // Prepare the chat request with financial document context
            var contextualPrompt = $"""
                You are a financial legal assistant analyzing SEC documents. Your job is to help with financial legal questions about SEC filings.
                You are not a lawyer and cannot give legal advice. You can only provide information and resources.
                
                Focus on these document types: 10-Q, 10-K, 8-K, DEF 14A and other SEC filings.
                
                Guidelines:
                - Extract important information from financial documents while preserving meaning
                - Keep the sentiment of the text and don't change meaning
                - Don't add or remove information from the source text
                - If you don't know something, say "I don't know"
                - Provide answers in paragraph format without bullet points
                - Don't use special characters, HTML tags, markdown, or code blocks
                
                User question: {userInput}
                """;

            var requestBody = new
            {
                message = new
                {
                    text = contextualPrompt
                },
                additionalContext = additionalContext?.Select(context => new
                {
                    text = context,
                    description = "SEC document context"
                }).ToArray(),
                locationHint = new
                {
                    timeZone = TimeZoneInfo.Local.Id
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending chat request to M365 Copilot for conversation {ConversationId}", conversationId);

            // Send synchronous chat request
            var response = await _httpClient.PostAsync($"{_baseUrl}/copilot/conversations/{conversationId}/chat", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("M365 Copilot API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"M365 Copilot API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var copilotResponse = JsonSerializer.Deserialize<CopilotConversationResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            // Extract the Copilot's response message (last message that's not from user)
            var copilotMessage = copilotResponse?.Messages?.LastOrDefault(m => 
                m.Text != null && !m.Text.Equals(userInput, StringComparison.OrdinalIgnoreCase));

            return copilotMessage?.Text ?? "No response received from M365 Copilot.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from M365 Copilot");
            throw;
        }
    }

    public async Task<string> CreateConversationAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("M365 access token is not configured.");
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            
            _logger.LogInformation("Creating new M365 Copilot conversation");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/copilot/conversations", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create M365 Copilot conversation: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to create conversation: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var conversation = JsonSerializer.Deserialize<CopilotConversationResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Created M365 Copilot conversation with ID: {ConversationId}", conversation?.Id);
            
            return conversation?.Id ?? throw new InvalidOperationException("Failed to get conversation ID from response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating M365 Copilot conversation");
            throw;
        }
    }

    // Legacy method for backward compatibility - now async
    public string GetChatResponse(string userInput)
    {
        return GetChatResponseAsync(userInput).GetAwaiter().GetResult();
    }
}

// Data models for M365 Copilot Chat API
public class CopilotConversationResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTimeOffset CreatedDateTime { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("turnCount")]
    public int TurnCount { get; set; }

    [JsonPropertyName("messages")]
    public List<CopilotConversationMessage>? Messages { get; set; }
}

public class CopilotConversationMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTimeOffset CreatedDateTime { get; set; }

    [JsonPropertyName("adaptiveCards")]
    public List<object>? AdaptiveCards { get; set; }

    [JsonPropertyName("attributions")]
    public List<CopilotAttribution>? Attributions { get; set; }
}

public class CopilotAttribution
{
    [JsonPropertyName("attributionType")]
    public string? AttributionType { get; set; }

    [JsonPropertyName("providerDisplayName")]
    public string? ProviderDisplayName { get; set; }

    [JsonPropertyName("attributionSource")]
    public string? AttributionSource { get; set; }

    [JsonPropertyName("seeMoreWebUrl")]
    public string? SeeMoreWebUrl { get; set; }
}
