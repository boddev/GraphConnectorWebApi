namespace ApiGraphActivator;

public class Company
{
    public int Cik { get; set; }
    public string Ticker { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime? LastCrawledDate { get; set; }
}

public class CrawlRequest
{
    public List<Company> Companies { get; set; } = new();
}


// M365 Copilot Models
public class CopilotConversation
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string Status { get; set; } = "Active";
    public List<CopilotMessage> Messages { get; set; } = new();
}

public class CopilotMessage
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CopilotChatRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public Dictionary<string, object> Context { get; set; } = new();
}

public class CopilotChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "assistant";
    public DateTime Timestamp { get; set; }
    public bool IsComplete { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CopilotStreamChunk
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsComplete { get; set; } = false;
    public string Type { get; set; } = "content"; // "content", "metadata", "error"
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CreateConversationRequest
{
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}

public class CopilotError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
/// <summary>
/// Represents a conversation session for multi-turn interactions
/// </summary>
public class ConversationSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public ConversationSessionStatus Status { get; set; } = ConversationSessionStatus.Active;
}

/// <summary>
/// Represents a conversation within a session
/// </summary>
public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public List<ConversationMessage> Messages { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
}

/// <summary>
/// Represents a message within a conversation
/// </summary>
public class ConversationMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public ConversationMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<DocumentCitation>? Citations { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}

/// <summary>
/// Represents a document citation in a conversation message
/// </summary>
public class DocumentCitation
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime FilingDate { get; set; }
    public string? RelevantExcerpt { get; set; }
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Status of a conversation session
/// </summary>
public enum ConversationSessionStatus
{
    Active,
    Inactive,
    Expired,
    Archived
}

/// <summary>
/// Status of a conversation
/// </summary>
public enum ConversationStatus
{
    Active,
    Completed,
    Archived
}

/// <summary>
/// Role of a message in a conversation
/// </summary>
public enum ConversationMessageRole
{
    User,
    Assistant,
    System,
    Tool
}

/// <summary>
/// Request model for creating a conversation session
/// </summary>
public class CreateSessionRequest
{
    public string? UserId { get; set; }
    public int? TtlHours { get; set; }
}

/// <summary>
/// Request model for creating a conversation
/// </summary>
public class CreateConversationRequest
{
    public string? Title { get; set; }
}

/// <summary>
/// Request model for adding a message to a conversation
/// </summary>
public class AddMessageRequest
{
    public ConversationMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<DocumentCitation>? Citations { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}
