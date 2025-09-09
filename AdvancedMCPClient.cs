// Advanced MCP Client for testing and integration
// File: AdvancedMCPClient.cs

using System.Text;
using System.Text.Json;

namespace ApiGraphActivator.Tests
{
    public class AdvancedMCPClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<AdvancedMCPClient>? _logger;

        public AdvancedMCPClient(string baseUrl = "http://localhost:5236", ILogger<AdvancedMCPClient>? logger = null)
        {
            _baseUrl = baseUrl;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task<MCPClientResult<T>> CallToolAsync<T>(string toolName, object arguments, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = arguments
                }
            };

            return await SendRequestAsync<T>(request, cancellationToken);
        }

        public async Task<MCPClientResult<ToolsListResponse>> GetToolsAsync(CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/list",
                @params = new { }
            };

            return await SendRequestAsync<ToolsListResponse>(request, cancellationToken);
        }

        public async Task<MCPClientResult<InitializeResponse>> InitializeAsync(CancellationToken cancellationToken = default)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "initialize",
                @params = new { }
            };

            return await SendRequestAsync<InitializeResponse>(request, cancellationToken);
        }

        public async Task<MCPSearchResult> SearchDocumentsAsync(string query, string? company = null, string? formType = null)
        {
            var arguments = new
            {
                query = query,
                company = company,
                formType = formType
            };

            var result = await CallToolAsync<object>("search_documents", arguments);
            return MCPSearchResult.FromResponse(result);
        }

        public async Task<MCPDocumentResult> GetDocumentContentAsync(string documentId)
        {
            var arguments = new { documentId = documentId };
            var result = await CallToolAsync<object>("get_document_content", arguments);
            return MCPDocumentResult.FromResponse(result);
        }

        public async Task<MCPStatusResult> GetCrawlStatusAsync()
        {
            var result = await CallToolAsync<object>("get_crawl_status", new { });
            return MCPStatusResult.FromResponse(result);
        }

        private async Task<MCPClientResult<T>> SendRequestAsync<T>(object request, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger?.LogDebug("Sending MCP request: {Request}", json);

                var response = await _httpClient.PostAsync($"{_baseUrl}/mcp", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger?.LogDebug("Received MCP response: {Response}", responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    return MCPClientResult<T>.Failure($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                var mcpResponse = JsonSerializer.Deserialize<MCPResponseWrapper>(responseBody);
                
                if (mcpResponse?.Error != null)
                {
                    return MCPClientResult<T>.Failure($"MCP Error {mcpResponse.Error.Code}: {mcpResponse.Error.Message}");
                }

                if (mcpResponse?.Result != null)
                {
                    var result = JsonSerializer.Deserialize<T>(mcpResponse.Result.ToString());
                    return MCPClientResult<T>.Success(result);
                }

                return MCPClientResult<T>.Failure("Empty response");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending MCP request");
                return MCPClientResult<T>.Failure($"Exception: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Response models
    public class MCPClientResult<T>
    {
        public bool IsSuccess { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }

        public static MCPClientResult<T> Success(T? data) => new() { IsSuccess = true, Data = data };
        public static MCPClientResult<T> Failure(string error) => new() { IsSuccess = false, Error = error };
    }

    public class MCPResponseWrapper
    {
        public JsonElement? Result { get; set; }
        public MCPError? Error { get; set; }
    }

    public class ToolsListResponse
    {
        public Tool[] Tools { get; set; } = Array.Empty<Tool>();
    }

    public class Tool
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public object InputSchema { get; set; } = new();
    }

    public class InitializeResponse
    {
        public string ProtocolVersion { get; set; } = "";
        public object Capabilities { get; set; } = new();
        public object ServerInfo { get; set; } = new();
    }

    // Specialized result types
    public class MCPSearchResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public List<DocumentInfo>? Documents { get; set; }

        public static MCPSearchResult FromResponse(MCPClientResult<object> response)
        {
            if (!response.IsSuccess)
                return new MCPSearchResult { IsSuccess = false, Error = response.Error };

            // Parse the response data to extract documents
            // Implementation depends on your actual response format
            return new MCPSearchResult { IsSuccess = true, Documents = new List<DocumentInfo>() };
        }
    }

    public class MCPDocumentResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string? Content { get; set; }

        public static MCPDocumentResult FromResponse(MCPClientResult<object> response)
        {
            if (!response.IsSuccess)
                return new MCPDocumentResult { IsSuccess = false, Error = response.Error };

            // Parse the response data to extract content
            return new MCPDocumentResult { IsSuccess = true, Content = "Document content here" };
        }
    }

    public class MCPStatusResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string? Status { get; set; }
        public int? Progress { get; set; }

        public static MCPStatusResult FromResponse(MCPClientResult<object> response)
        {
            if (!response.IsSuccess)
                return new MCPStatusResult { IsSuccess = false, Error = response.Error };

            return new MCPStatusResult { IsSuccess = true, Status = "Active", Progress = 75 };
        }
    }

    public class DocumentInfo
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Company { get; set; } = "";
        public string FormType { get; set; } = "";
        public DateTime Date { get; set; }
        public string Url { get; set; } = "";
    }
}
