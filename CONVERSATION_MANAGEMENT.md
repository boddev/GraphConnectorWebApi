# Conversation Management System (MCP-203)

## Overview

The Conversation Management System enables multi-turn interactions with M365 Copilot through MCP (Model Context Protocol) tools. This system provides session-based conversation isolation, context preservation, and comprehensive conversation history management.

## Architecture

### Core Components

1. **ConversationSession**: Manages user sessions with TTL and isolation
2. **Conversation**: Contains multi-turn conversation threads
3. **ConversationMessage**: Individual messages with role-based typing
4. **DocumentCitation**: Structured citations linking to SEC documents
5. **ConversationService**: Business logic for conversation management
6. **Storage Integration**: Extended storage interfaces for persistence

### Data Models

#### ConversationSession
```csharp
public class ConversationSession
{
    public string Id { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public ConversationSessionStatus Status { get; set; }
}
```

#### Conversation
```csharp
public class Conversation
{
    public string Id { get; set; }
    public string SessionId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public List<ConversationMessage> Messages { get; set; }
    public Dictionary<string, object> Context { get; set; }
    public ConversationStatus Status { get; set; }
}
```

#### ConversationMessage
```csharp
public class ConversationMessage
{
    public string Id { get; set; }
    public string ConversationId { get; set; }
    public ConversationMessageRole Role { get; set; } // User, Assistant, System, Tool
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public List<DocumentCitation>? Citations { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}
```

#### DocumentCitation
```csharp
public class DocumentCitation
{
    public string DocumentId { get; set; }
    public string DocumentTitle { get; set; }
    public string CompanyName { get; set; }
    public string FormType { get; set; }
    public string Url { get; set; }
    public DateTime FilingDate { get; set; }
    public string? RelevantExcerpt { get; set; }
    public double RelevanceScore { get; set; }
}
```

## API Endpoints

### Session Management

#### Create Session
```http
POST /conversations/sessions
Content-Type: application/json

{
  "userId": "user-123",
  "ttlHours": 24
}

Response: ConversationSession
```

#### Get Session
```http
GET /conversations/sessions/{sessionId}

Response: ConversationSession
```

### Conversation Management

#### Create Conversation
```http
POST /conversations/sessions/{sessionId}/conversations
Content-Type: application/json

{
  "title": "SEC Filing Analysis"
}

Response: Conversation
```

#### Get Conversation with Messages
```http
GET /conversations/{conversationId}?skip=0&take=100

Response: ConversationWithMessages
```

#### List Session Conversations
```http
GET /conversations/sessions/{sessionId}/conversations

Response: List<Conversation>
```

### Message Management

#### Add Message
```http
POST /conversations/{conversationId}/messages
Content-Type: application/json

{
  "role": 0, // User=0, Assistant=1, System=2, Tool=3
  "content": "What are Apple's latest 10-K filings?",
  "citations": [
    {
      "documentId": "apple-10k-2023",
      "documentTitle": "Apple Inc. Form 10-K",
      "companyName": "Apple Inc.",
      "formType": "10-K",
      "url": "https://sec.gov/...",
      "filingDate": "2023-11-03T00:00:00Z",
      "relevantExcerpt": "...",
      "relevanceScore": 0.95
    }
  ],
  "metadata": {
    "searchQuery": "Apple 10-K",
    "resultsCount": 1
  },
  "toolCallId": "search-123",
  "toolName": "search_documents_by_company"
}

Response: ConversationMessage
```

### Context Management

#### Update Conversation Context
```http
PUT /conversations/{conversationId}/context
Content-Type: application/json

{
  "lastSearchQuery": "Apple 10-K",
  "documentsRetrieved": 3,
  "userInterest": "financial data",
  "searchFilters": {
    "company": "Apple Inc.",
    "formType": "10-K"
  }
}

Response: { "message": "Context updated successfully" }
```

### System Management

#### Get Metrics
```http
GET /conversations/metrics

Response: ConversationMetrics
```

#### Cleanup Expired Sessions
```http
POST /conversations/cleanup

Response: { "message": "Cleanup completed successfully" }
```

## Usage Examples

### Complete Conversation Flow

```bash
# 1. Create session
SESSION_RESPONSE=$(curl -X POST "http://localhost:5236/conversations/sessions" \
  -H "Content-Type: application/json" \
  -d '{"userId": "analyst-1", "ttlHours": 8}')

SESSION_ID=$(echo $SESSION_RESPONSE | jq -r '.id')

# 2. Create conversation
CONV_RESPONSE=$(curl -X POST "http://localhost:5236/conversations/sessions/$SESSION_ID/conversations" \
  -H "Content-Type: application/json" \
  -d '{"title": "Apple Financial Analysis"}')

CONV_ID=$(echo $CONV_RESPONSE | jq -r '.id')

# 3. Add user message
curl -X POST "http://localhost:5236/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 0,
    "content": "Show me Apple recent 10-K filings and summarize key risks"
  }'

# 4. Add assistant response with citations
curl -X POST "http://localhost:5236/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 1,
    "content": "I found Apple 10-K filing from 2023. Key risks include supply chain dependencies...",
    "citations": [
      {
        "documentId": "aapl-10k-2023",
        "documentTitle": "Apple Inc. 2023 Form 10-K",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "url": "https://sec.gov/...",
        "filingDate": "2023-11-03T00:00:00Z",
        "relevantExcerpt": "Risk Factors: We are subject to supply chain risks...",
        "relevanceScore": 0.92
      }
    ],
    "toolCallId": "search-001",
    "toolName": "search_documents_by_company"
  }'

# 5. Get full conversation
curl -X GET "http://localhost:5236/conversations/$CONV_ID"
```

### Integration with MCP Document Search

The conversation system integrates seamlessly with existing MCP document search tools:

```csharp
// In a conversation-aware document search
var searchResults = await _documentSearchService.SearchByCompanyAsync(parameters);

// Add context to conversation
await _conversationService.UpdateConversationContextAsync(conversationId, new Dictionary<string, object>
{
    ["lastSearchQuery"] = parameters.CompanyName,
    ["searchResults"] = searchResults.Items.Count,
    ["searchTimestamp"] = DateTime.UtcNow
});

// Add assistant message with citations
var citations = searchResults.Items.Select(doc => new DocumentCitation
{
    DocumentId = doc.Id,
    DocumentTitle = doc.Title,
    CompanyName = doc.CompanyName,
    FormType = doc.FormType,
    Url = doc.Url,
    FilingDate = doc.FilingDate,
    RelevanceScore = doc.RelevanceScore
}).ToList();

await _conversationService.AddMessageAsync(
    conversationId,
    ConversationMessageRole.Assistant,
    "I found the following SEC filings...",
    citations,
    metadata: new Dictionary<string, object> { ["searchQuery"] = parameters.CompanyName }
);
```

## Storage Implementation

### In-Memory Storage (Default)
- Singleton instance for session persistence
- Thread-safe with locking mechanisms
- Automatic session expiration handling
- Suitable for development and testing

### Extension Points for Production Storage
- Azure Storage Service (stub implementation provided)
- Local File Storage (stub implementation provided)
- Custom storage implementations via ICrawlStorageService interface

### Storage Interface Extensions

```csharp
public interface ICrawlStorageService
{
    // Session management
    Task<ConversationSession> CreateSessionAsync(string? userId = null, TimeSpan? ttl = null);
    Task<ConversationSession?> GetSessionAsync(string sessionId);
    Task UpdateSessionAsync(ConversationSession session);
    Task DeleteSessionAsync(string sessionId);
    Task<List<ConversationSession>> GetUserSessionsAsync(string userId);
    Task CleanupExpiredSessionsAsync();
    
    // Conversation management
    Task<Conversation> CreateConversationAsync(string sessionId, string? title = null);
    Task<Conversation?> GetConversationAsync(string conversationId);
    Task UpdateConversationAsync(Conversation conversation);
    Task DeleteConversationAsync(string conversationId);
    Task<List<Conversation>> GetSessionConversationsAsync(string sessionId);
    
    // Message management
    Task<ConversationMessage> AddMessageAsync(string conversationId, ConversationMessageRole role, 
        string content, List<DocumentCitation>? citations = null, Dictionary<string, object>? metadata = null);
    Task<List<ConversationMessage>> GetConversationMessagesAsync(string conversationId, int skip = 0, int take = 100);
    Task UpdateMessageAsync(ConversationMessage message);
}
```

## Configuration

### Storage Configuration
```json
{
  "provider": "Memory",
  "localDataPath": "./data",
  "autoCreateTables": true
}
```

### Service Registration
```csharp
// In Program.cs
builder.Services.AddScoped<ConversationService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<ConversationService>>();
    var storageConfigService = serviceProvider.GetRequiredService<StorageConfigurationService>();
    var storageService = storageConfigService.GetStorageServiceAsync().GetAwaiter().GetResult();
    var documentSearchService = serviceProvider.GetRequiredService<DocumentSearchService>();
    return new ConversationService(logger, storageService, documentSearchService);
});
```

## Monitoring and Metrics

### Conversation Metrics
```csharp
public class ConversationMetrics
{
    public int ActiveSessions { get; set; }
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public DateTime LastCleanup { get; set; }
    public Dictionary<string, int> MessagesByRole { get; set; }
    public Dictionary<string, int> ToolUsage { get; set; }
}
```

### Health Monitoring
- Session expiration tracking
- Conversation activity metrics
- Message volume monitoring
- Tool usage analytics
- Storage health checks

## Security Considerations

### Session Security
- Session IDs are GUIDs for unguessability
- TTL-based session expiration
- User-based session isolation
- No sensitive data in session metadata

### Access Control
- Session ownership validation
- Conversation access via session membership
- Message history protection
- Context data encryption ready

## Performance Considerations

### Scalability Features
- Pagination support for message retrieval
- Context-based conversation lookup
- Efficient session cleanup
- Background task integration

### Optimization Opportunities
- Message content compression
- Citation deduplication
- Context summarization
- Archived conversation storage

## Future Enhancements

### Planned Features
1. **Conversation Search**: Full-text search across conversation history
2. **Template Support**: Pre-defined conversation templates
3. **Export Functions**: Conversation export to various formats
4. **Analytics Dashboard**: Advanced conversation analytics
5. **Auto-archival**: Intelligent conversation archival
6. **Webhook Integration**: Real-time conversation events

### Integration Roadmap
1. **Azure Storage**: Full Azure Table/Blob storage implementation
2. **Redis Cache**: Distributed session caching
3. **Event Streaming**: Real-time conversation events
4. **AI Integration**: Conversation summarization and insights
5. **Multi-tenant**: Enterprise multi-tenant support

## Testing

### Manual Testing Completed
- ✅ Session creation and retrieval
- ✅ Conversation lifecycle management
- ✅ Multi-turn message flows
- ✅ Citation and metadata handling
- ✅ Context preservation and updates
- ✅ Session isolation verification
- ✅ Cleanup and expiration handling

### Test Coverage Areas
- API endpoint validation
- Business logic verification
- Storage integration testing
- Error handling validation
- Performance benchmarking
- Security penetration testing

## Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| Session Management | ✅ Complete | Full CRUD operations |
| Conversation Management | ✅ Complete | Multi-turn support |
| Message Handling | ✅ Complete | All roles and metadata |
| Citation System | ✅ Complete | SEC document linking |
| Context Management | ✅ Complete | Dynamic context updates |
| Storage Integration | ✅ Complete | InMemory implementation |
| API Endpoints | ✅ Complete | RESTful interface |
| Documentation | ✅ Complete | Comprehensive docs |
| Testing | ✅ Complete | Manual validation |
| Production Storage | 🔄 Partial | Stubs for Azure/File |

The conversation management system successfully implements all requirements from MCP-203, providing a robust foundation for multi-turn interactions with M365 Copilot through MCP tools.