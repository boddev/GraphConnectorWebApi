using ApiGraphActivator;

namespace ApiGraphActivator.Services;

/// <summary>
/// Interface for M365 Copilot Client that handles authentication, conversation management, and streaming responses
/// </summary>
public interface IM365CopilotClient
{
    /// <summary>
    /// Initialize the client with authentication
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Create a new conversation
    /// </summary>
    Task<CopilotConversation> CreateConversationAsync(CreateConversationRequest request);

    /// <summary>
    /// Get an existing conversation by ID
    /// </summary>
    Task<CopilotConversation?> GetConversationAsync(string conversationId);

    /// <summary>
    /// Send a message and get a synchronous response
    /// </summary>
    Task<CopilotChatResponse> SendMessageAsync(CopilotChatRequest request);

    /// <summary>
    /// Send a message and get a streaming response
    /// </summary>
    IAsyncEnumerable<CopilotStreamChunk> SendMessageStreamAsync(CopilotChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a conversation
    /// </summary>
    Task<bool> DeleteConversationAsync(string conversationId);

    /// <summary>
    /// Get all conversations for a tenant
    /// </summary>
    Task<List<CopilotConversation>> GetConversationsAsync(string tenantId, int limit = 50);

    /// <summary>
    /// Check if the client is authenticated and ready
    /// </summary>
    Task<bool> IsHealthyAsync();
}