using Azure.Identity;
using Microsoft.Graph;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using ApiGraphActivator;

namespace ApiGraphActivator.Services;

/// <summary>
/// M365 Copilot Client implementation that handles authentication, conversation management, and streaming responses
/// </summary>
public class M365CopilotClient : IM365CopilotClient
{
    private readonly ILogger<M365CopilotClient> _logger;
    private readonly HttpClient _httpClient;
    private GraphServiceClient? _graphClient;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    
    // Configuration
    private readonly string _baseUrl = "https://graph.microsoft.com/v1.0";
    private readonly string _copilotBaseUrl = "https://api.copilot.microsoft.com/v1.0"; // Placeholder URL
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
    
    // In-memory conversation storage (in production, use persistent storage)
    private readonly Dictionary<string, CopilotConversation> _conversations = new();

    public M365CopilotClient(ILogger<M365CopilotClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for streaming
    }

    /// <summary>
    /// Initialize the client with authentication
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing M365 Copilot Client...");

            // Get authentication credentials from environment variables
            var clientId = Environment.GetEnvironmentVariable("AzureAd:ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("AzureAd:ClientSecret");
            var tenantId = Environment.GetEnvironmentVariable("AzureAd:TenantId");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("Missing Azure AD configuration. Running in demo mode without real authentication.");
                // For demo purposes, simulate successful initialization
                _accessToken = "demo_token_" + Guid.NewGuid().ToString("N")[..16];
                _tokenExpiry = DateTime.UtcNow.AddHours(1);
                _logger.LogInformation("M365 Copilot Client initialized in demo mode");
                return true;
            }

            // Create credential and Graph client
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            _graphClient = new GraphServiceClient(credential);

            // Test authentication by getting an access token
            await RefreshTokenAsync();

            _logger.LogInformation("M365 Copilot Client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize M365 Copilot Client");
            return false;
        }
    }

    /// <summary>
    /// Create a new conversation
    /// </summary>
    public async Task<CopilotConversation> CreateConversationAsync(CreateConversationRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new conversation for tenant: {TenantId}", request.TenantId);

            var conversation = new CopilotConversation
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = request.TenantId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                Status = "Active"
            };

            // Store conversation in memory (in production, use persistent storage)
            _conversations[conversation.Id] = conversation;

            _logger.LogInformation("Created conversation with ID: {ConversationId}", conversation.Id);
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation for tenant: {TenantId}", request.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Get an existing conversation by ID
    /// </summary>
    public Task<CopilotConversation?> GetConversationAsync(string conversationId)
    {
        try
        {
            _logger.LogDebug("Getting conversation: {ConversationId}", conversationId);
            
            _conversations.TryGetValue(conversationId, out var conversation);
            return Task.FromResult(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation: {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Send a message and get a synchronous response
    /// </summary>
    public async Task<CopilotChatResponse> SendMessageAsync(CopilotChatRequest request)
    {
        try
        {
            _logger.LogInformation("Sending message to conversation: {ConversationId}", request.ConversationId);

            // Get or create conversation
            var conversation = await GetConversationAsync(request.ConversationId);
            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {request.ConversationId} not found");
            }

            // Add user message to conversation
            var userMessage = new CopilotMessage
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = request.ConversationId,
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            };
            conversation.Messages.Add(userMessage);

            // Simulate calling M365 Copilot API with retry logic
            var response = await ExecuteWithRetryAsync(async () =>
            {
                await EnsureAuthenticatedAsync();
                return await CallCopilotApiAsync(request);
            });

            // Add assistant message to conversation
            var assistantMessage = new CopilotMessage
            {
                Id = response.Id,
                ConversationId = request.ConversationId,
                Role = "assistant",
                Content = response.Content,
                Timestamp = response.Timestamp
            };
            conversation.Messages.Add(assistantMessage);
            conversation.LastMessageAt = DateTime.UtcNow;

            _logger.LogInformation("Message sent successfully to conversation: {ConversationId}", request.ConversationId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to conversation: {ConversationId}", request.ConversationId);
            throw;
        }
    }

    /// <summary>
    /// Send a message and get a streaming response
    /// </summary>
    public async IAsyncEnumerable<CopilotStreamChunk> SendMessageStreamAsync(CopilotChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting streaming message to conversation: {ConversationId}", request.ConversationId);

        // Get or create conversation
        var conversation = await GetConversationAsync(request.ConversationId);
        if (conversation == null)
        {
            throw new InvalidOperationException($"Conversation {request.ConversationId} not found");
        }

        // Add user message to conversation
        var userMessage = new CopilotMessage
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = request.ConversationId,
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow
        };
        conversation.Messages.Add(userMessage);

        var responseId = Guid.NewGuid().ToString();
        var fullContent = new StringBuilder();

        // Stream response without try-catch around yield statements
        await foreach (var chunk in CallCopilotStreamApiAsync(request, responseId, cancellationToken))
        {
            if (chunk.Type == "content")
            {
                fullContent.Append(chunk.Content);
            }
            
            yield return chunk;
            
            if (chunk.IsComplete)
            {
                break;
            }
        }

        // Add complete assistant message to conversation
        var assistantMessage = new CopilotMessage
        {
            Id = responseId,
            ConversationId = request.ConversationId,
            Role = "assistant",
            Content = fullContent.ToString(),
            Timestamp = DateTime.UtcNow
        };
        conversation.Messages.Add(assistantMessage);
        conversation.LastMessageAt = DateTime.UtcNow;

        _logger.LogInformation("Streaming message completed for conversation: {ConversationId}", request.ConversationId);
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    public async Task<bool> DeleteConversationAsync(string conversationId)
    {
        try
        {
            _logger.LogInformation("Deleting conversation: {ConversationId}", conversationId);
            
            var removed = _conversations.Remove(conversationId);
            
            if (removed)
            {
                _logger.LogInformation("Conversation deleted successfully: {ConversationId}", conversationId);
            }
            else
            {
                _logger.LogWarning("Conversation not found for deletion: {ConversationId}", conversationId);
            }
            
            return await Task.FromResult(removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation: {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Get all conversations for a tenant
    /// </summary>
    public async Task<List<CopilotConversation>> GetConversationsAsync(string tenantId, int limit = 50)
    {
        try
        {
            _logger.LogDebug("Getting conversations for tenant: {TenantId}, limit: {Limit}", tenantId, limit);
            
            var conversations = _conversations.Values
                .Where(c => c.TenantId == tenantId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(limit)
                .ToList();
            
            return await Task.FromResult(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversations for tenant: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Check if the client is authenticated and ready
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if token is valid
            if (DateTime.UtcNow >= _tokenExpiry)
            {
                await RefreshTokenAsync();
            }

            return !string.IsNullOrEmpty(_accessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return false;
        }
    }

    #region Private Methods

    private async Task RefreshTokenAsync()
    {
        try
        {
            // For this implementation, we'll simulate token management
            // In a real implementation, you would use the authentication provider properly
            _accessToken = "simulated_token_" + Guid.NewGuid().ToString("N")[..16];
            _tokenExpiry = DateTime.UtcNow.AddHours(1);

            _logger.LogDebug("Access token refreshed, expires at: {TokenExpiry}", _tokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh access token");
            throw;
        }
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (DateTime.UtcNow >= _tokenExpiry || string.IsNullOrEmpty(_accessToken))
        {
            await RefreshTokenAsync();
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var retryCount = 0;
        while (retryCount < _maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (retryCount < _maxRetries - 1 && IsRetryableException(ex))
            {
                retryCount++;
                var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1));
                
                _logger.LogWarning(ex, "Operation failed, retrying in {Delay}ms (attempt {RetryCount}/{MaxRetries})", 
                    delay.TotalMilliseconds, retryCount, _maxRetries);
                
                await Task.Delay(delay);
            }
        }

        // Final attempt without catching exceptions
        return await operation();
    }

    private bool IsRetryableException(Exception ex)
    {
        // Determine if the exception is retryable
        return ex is HttpRequestException ||
               ex is TaskCanceledException ||
               (ex is InvalidOperationException && ex.Message.Contains("authentication"));
    }

    private async Task<CopilotChatResponse> CallCopilotApiAsync(CopilotChatRequest request)
    {
        // This is a simulation of calling the actual M365 Copilot API
        // In a real implementation, you would make HTTP calls to the actual Copilot endpoints
        
        _logger.LogDebug("Simulating Copilot API call for conversation: {ConversationId}", request.ConversationId);
        
        // Simulate API delay
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        
        // Generate simulated response
        var response = new CopilotChatResponse
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = request.ConversationId,
            Content = $"This is a simulated response to: '{request.Message}'. In a real implementation, this would be generated by M365 Copilot.",
            Role = "assistant",
            Timestamp = DateTime.UtcNow,
            IsComplete = true,
            Metadata = new Dictionary<string, object>
            {
                ["model"] = "gpt-4",
                ["tokens_used"] = 150,
                ["response_time_ms"] = 500
            }
        };

        return response;
    }

    private async IAsyncEnumerable<CopilotStreamChunk> CallCopilotStreamApiAsync(
        CopilotChatRequest request, 
        string responseId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // This is a simulation of calling the actual M365 Copilot streaming API
        // In a real implementation, you would make streaming HTTP calls to the actual Copilot endpoints
        
        _logger.LogDebug("Simulating Copilot streaming API call for conversation: {ConversationId}", request.ConversationId);
        
        var responseText = $"This is a simulated streaming response to: '{request.Message}'. In a real implementation, this would be streamed from M365 Copilot.";
        var words = responseText.Split(' ');
        
        // Simulate streaming by yielding words with delays
        for (int i = 0; i < words.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var chunk = new CopilotStreamChunk
            {
                Id = responseId,
                ConversationId = request.ConversationId,
                Content = words[i] + (i < words.Length - 1 ? " " : ""),
                Type = "content",
                IsComplete = i == words.Length - 1
            };
            
            yield return chunk;
            
            // Simulate streaming delay
            await Task.Delay(100, cancellationToken);
        }
    }

    #endregion
}