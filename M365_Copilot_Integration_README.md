# M365 Copilot Chat Integration

This project now includes integration with Microsoft 365 Copilot Chat API for analyzing SEC documents.

## Setup

### 1. Azure App Registration

1. Go to Azure Portal → App registrations
2. Create a new app registration or use an existing one
3. Add the following API permissions under "API permissions":
   - `Sites.Read.All`
   - `Mail.Read`
   - `People.Read.All`
   - `OnlineMeetingTranscript.Read.All`
   - `Chat.Read`
   - `ChannelMessage.Read.All`
   - `ExternalItem.Read.All`

### 2. Authentication

Set up authentication to get an access token for the signed-in user:

1. Use delegated permissions (user authentication)
2. Include all the required scopes in your authentication request
3. Store the access token securely

### 3. Environment Variables

Set the following environment variable:

```
M365_ACCESS_TOKEN=your_bearer_token_here
```

**Important**: This should be a valid bearer token for a user with M365 Copilot license.

## Usage

### Basic Chat

```csharp
var copilotService = new CopilotChatService(logger, httpClient);
var response = await copilotService.GetChatResponseAsync("Analyze recent financial documents for risk factors");
```

### Document Analysis via MCP

The MCP Server exposes an `analyze_document` tool that uses M365 Copilot:

```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "tools/call",
  "params": {
    "name": "analyze_document",
    "arguments": {
      "documentId": "doc123",
      "analysisType": "financial"
    }
  }
}
```

### Analysis Types

- `financial`: Analyzes financial metrics, revenue, profit, cash flow
- `risk`: Identifies and analyzes risk factors
- `governance`: Analyzes corporate governance information
- `summary`: Provides comprehensive summary
- Custom types: Any other string for general analysis

## API Endpoints

### MCP Server
- `POST /mcp` - Main MCP protocol endpoint

### Available MCP Tools
- `search_documents` - Search SEC documents
- `get_document_content` - Retrieve document content
- `analyze_document` - AI-powered document analysis using M365 Copilot
- `list_companies` - List tracked companies

## Authentication Flow

1. User authenticates with Azure AD
2. Application receives delegated access token
3. Token is used to authenticate with M365 Copilot Chat API
4. Copilot responses are processed and returned through MCP

## Error Handling

The service includes comprehensive error handling for:
- Missing or invalid access tokens
- M365 Copilot API errors
- Network connectivity issues
- JSON serialization/deserialization errors

## Security Considerations

- Access tokens should be stored securely and rotated regularly
- All M365 data access honors existing security, compliance, and governance policies
- Users must have appropriate M365 Copilot licenses
- The service runs in the context of the authenticated user

## Testing

Use the included `MCPTestClient.cs` to test the MCP server functionality:

```csharp
var client = new MCPTestClient();
await client.TestSearchDocuments("financial statements", "Apple");
```

## Dependencies

- .NET 8.0+
- Microsoft.Extensions.Logging
- System.Text.Json
- HttpClient
- Valid M365 Copilot license for users
