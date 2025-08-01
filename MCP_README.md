# MCP Server Implementation

This project now includes a Model Context Protocol (MCP) Server that allows AI assistants like Claude to interact with your SEC Edgar document processing system.

## What is MCP?

Model Context Protocol (MCP) is a standardized way for AI assistants to communicate with external tools and data sources. It enables Claude and other AI assistants to access real-time information and perform actions through your APIs.

## MCP Server Features

The MCP Server provides the following tools:

### Tools Available

1. **search_documents**
   - Search SEC documents by company, form type, or content
   - Parameters: `query` (required), `company` (optional), `formType` (optional), `dateRange` (optional)

2. **get_document_content**
   - Retrieve full content of a specific SEC document
   - Parameters: `documentId` (required)

3. **analyze_document**
   - Analyze SEC document content using AI
   - Parameters: `documentId` (required), `analysisType` (required)

4. **list_companies**
   - List all tracked companies
   - Parameters: none

## How to Use

### 1. Start the API Server

First, make sure your API server is running:

```powershell
cd ApiGraphActivator
dotnet run
```

The server will start on `https://localhost:7000` by default.

### 2. Test the MCP Endpoint

You can test the MCP server using the provided test client:

```powershell
# Compile and run the test client
dotnet run --project MCPTestClient.cs
```

Or test manually with curl:

```powershell
# Test initialization
curl -X POST https://localhost:7000/mcp `
  -H "Content-Type: application/json" `
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "initialize",
    "params": {}
  }'

# Test tools list
curl -X POST https://localhost:7000/mcp `
  -H "Content-Type: application/json" `
  -d '{
    "jsonrpc": "2.0",
    "id": "2", 
    "method": "tools/list",
    "params": {}
  }'

# Test search documents
curl -X POST https://localhost:7000/mcp `
  -H "Content-Type: application/json" `
  -d '{
    "jsonrpc": "2.0",
    "id": "3",
    "method": "tools/call",
    "params": {
      "name": "search_documents",
      "arguments": {
        "query": "financial statements",
        "company": "Apple"
      }
    }
  }'
```

### 3. Configure Claude Desktop (Optional)

To use with Claude Desktop, you can configure it to use your MCP server. However, this typically requires a more complex setup with stdio communication. For now, the HTTP endpoint approach is simpler for testing.

## MCP Protocol Implementation

The server implements the core MCP protocol methods:

- `initialize` - Handshake and capability negotiation
- `tools/list` - List available tools
- `tools/call` - Execute a specific tool
- `resources/list` - List available resources (future)
- `resources/read` - Read resource content (future)

## Next Steps

1. **Integration with Existing Services**: Replace the mock responses with calls to your actual `EdgarService`, `OpenAIService`, etc.

2. **Add Authentication**: Implement proper authentication for the MCP endpoint.

3. **WebSocket Support**: Add WebSocket support for real-time communication.

4. **Resource Implementation**: Implement the resources endpoints for accessing documents directly.

5. **Error Handling**: Enhance error handling and validation.

## Example Integration

Here's how to integrate with your existing services:

```csharp
// In HandleSearchDocuments method
private async Task<MCPResponse> HandleSearchDocuments(string requestId, JsonElement arguments)
{
    try
    {
        var query = arguments.GetProperty("query").GetString();
        var company = arguments.TryGetProperty("company", out var companyProp) ? companyProp.GetString() : null;
        
        // Use your actual EdgarService
        var results = await EdgarService.SearchDocumentsAsync(query, company);
        
        return new MCPResponse
        {
            Id = requestId,
            Result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            }
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error searching documents");
        return new MCPResponse 
        { 
            Id = requestId, 
            Error = new MCPError { Code = -32603, Message = $"Search error: {ex.Message}" } 
        };
    }
}
```

## Files Modified/Added

- `ApiGraphActivator/Services/MCPServerService.cs` - Main MCP server implementation
- `ApiGraphActivator/Program.cs` - Added MCP service registration and endpoint
- `MCPTestClient.cs` - Test client for the MCP server
- `claude-mcp-config.json` - Example configuration for Claude Desktop
- `MCP_README.md` - This documentation

The MCP server is now ready for testing and can be extended to provide full integration with your Edgar document processing system.
