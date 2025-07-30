using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiGraphActivator.Services;

/// <summary>
/// Service for managing multi-turn conversations with context preservation
/// </summary>
public class ConversationService
{
    private readonly ILogger<ConversationService> _logger;
    private readonly ICrawlStorageService _storageService;
    private readonly DocumentSearchService _documentSearchService;
    private static readonly TimeSpan DefaultSessionTtl = TimeSpan.FromHours(24);
    private static readonly int MaxMessagesPerConversation = 1000;

    public ConversationService(
        ILogger<ConversationService> logger,
        ICrawlStorageService storageService,
        DocumentSearchService documentSearchService)
    {
        _logger = logger;
        _storageService = storageService;
        _documentSearchService = documentSearchService;
    }

    /// <summary>
    /// Creates a new conversation session
    /// </summary>
    public async Task<ConversationSession> CreateSessionAsync(string? userId = null, TimeSpan? ttl = null)
    {
        try
        {
            var session = await _storageService.CreateSessionAsync(userId, ttl ?? DefaultSessionTtl);
            
            _logger.LogInformation("Created conversation session {SessionId} for user {UserId}", 
                session.Id, userId ?? "anonymous");
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation session for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets an existing conversation session
    /// </summary>
    public async Task<ConversationSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            var session = await _storageService.GetSessionAsync(sessionId);
            
            if (session != null)
            {
                // Update last accessed time
                session.LastAccessedAt = DateTime.UtcNow;
                await _storageService.UpdateSessionAsync(session);
            }
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new conversation within a session
    /// </summary>
    public async Task<Conversation> CreateConversationAsync(string sessionId, string? title = null)
    {
        try
        {
            // Verify session exists
            var session = await _storageService.GetSessionAsync(sessionId);
            if (session == null)
            {
                throw new ArgumentException($"Session {sessionId} not found");
            }

            var conversation = await _storageService.CreateConversationAsync(sessionId, title);
            
            _logger.LogInformation("Created conversation {ConversationId} in session {SessionId}", 
                conversation.Id, sessionId);
            
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation in session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets a conversation with its messages
    /// </summary>
    public async Task<ConversationWithMessages?> GetConversationWithMessagesAsync(string conversationId, int skip = 0, int take = 100)
    {
        try
        {
            var conversation = await _storageService.GetConversationAsync(conversationId);
            if (conversation == null)
            {
                return null;
            }

            var messages = await _storageService.GetConversationMessagesAsync(conversationId, skip, take);
            
            return new ConversationWithMessages
            {
                Conversation = conversation,
                Messages = messages,
                HasMoreMessages = messages.Count == take
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation {ConversationId} with messages", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Adds a message to a conversation
    /// </summary>
    public async Task<ConversationMessage> AddMessageAsync(
        string conversationId,
        ConversationMessageRole role,
        string content,
        List<DocumentCitation>? citations = null,
        Dictionary<string, object>? metadata = null,
        string? toolCallId = null,
        string? toolName = null)
    {
        try
        {
            // Verify conversation exists
            var conversation = await _storageService.GetConversationAsync(conversationId);
            if (conversation == null)
            {
                throw new ArgumentException($"Conversation {conversationId} not found");
            }

            // Check message limit
            var existingMessages = await _storageService.GetConversationMessagesAsync(conversationId, 0, 1);
            var messageCount = existingMessages.Count;
            
            if (messageCount >= MaxMessagesPerConversation)
            {
                throw new InvalidOperationException($"Conversation {conversationId} has reached maximum message limit");
            }

            var message = await _storageService.AddMessageAsync(conversationId, role, content, citations, metadata);
            
            // Set tool-related properties if provided
            if (!string.IsNullOrEmpty(toolCallId))
            {
                message.ToolCallId = toolCallId;
            }
            if (!string.IsNullOrEmpty(toolName))
            {
                message.ToolName = toolName;
            }

            // Update conversation last message time
            conversation.LastMessageAt = message.Timestamp;
            await _storageService.UpdateConversationAsync(conversation);

            _logger.LogInformation("Added {Role} message to conversation {ConversationId}", 
                role, conversationId);
            
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message to conversation {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Gets all conversations for a session
    /// </summary>
    public async Task<List<Conversation>> GetSessionConversationsAsync(string sessionId)
    {
        try
        {
            return await _storageService.GetSessionConversationsAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversations for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Updates conversation context with search results or other relevant data
    /// </summary>
    public async Task UpdateConversationContextAsync(string conversationId, Dictionary<string, object> contextUpdates)
    {
        try
        {
            var conversation = await _storageService.GetConversationAsync(conversationId);
            if (conversation == null)
            {
                throw new ArgumentException($"Conversation {conversationId} not found");
            }

            foreach (var kvp in contextUpdates)
            {
                conversation.Context[kvp.Key] = kvp.Value;
            }

            await _storageService.UpdateConversationAsync(conversation);
            
            _logger.LogInformation("Updated context for conversation {ConversationId} with {Count} items", 
                conversationId, contextUpdates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update context for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Archives old conversations to manage storage
    /// </summary>
    public async Task ArchiveOldConversationsAsync(TimeSpan olderThan)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - olderThan;
            
            // This would need to be implemented in the storage service
            // For now, we'll just log the intent
            _logger.LogInformation("Archiving conversations older than {CutoffDate}", cutoffDate);
            
            // Implementation would involve:
            // 1. Find conversations with LastMessageAt < cutoffDate
            // 2. Change their status to Archived
            // 3. Optionally move to long-term storage
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive old conversations");
            throw;
        }
    }

    /// <summary>
    /// Cleans up expired sessions and their conversations
    /// </summary>
    public async Task CleanupExpiredSessionsAsync()
    {
        try
        {
            await _storageService.CleanupExpiredSessionsAsync();
            _logger.LogInformation("Cleaned up expired conversation sessions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired sessions");
            throw;
        }
    }

    /// <summary>
    /// Gets conversation statistics for monitoring
    /// </summary>
    public async Task<ConversationMetrics> GetConversationMetricsAsync()
    {
        try
        {
            // This would need to be implemented based on storage capabilities
            // For now, return basic metrics
            return new ConversationMetrics
            {
                ActiveSessions = 0,
                TotalConversations = 0,
                TotalMessages = 0,
                LastCleanup = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation metrics");
            throw;
        }
    }
}

/// <summary>
/// Wrapper for conversation with its messages
/// </summary>
public class ConversationWithMessages
{
    public Conversation Conversation { get; set; } = new();
    public List<ConversationMessage> Messages { get; set; } = new();
    public bool HasMoreMessages { get; set; }
}

/// <summary>
/// Metrics for conversation system monitoring
/// </summary>
public class ConversationMetrics
{
    public int ActiveSessions { get; set; }
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public DateTime LastCleanup { get; set; }
    public Dictionary<string, int> MessagesByRole { get; set; } = new();
    public Dictionary<string, int> ToolUsage { get; set; } = new();
}