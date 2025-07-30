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
