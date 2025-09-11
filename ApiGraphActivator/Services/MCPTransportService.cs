using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.Services
{
    /// <summary>
    /// Service for handling MCP protocol over stdio transport
    /// Allows direct MCP client communication without HTTP layer
    /// </summary>
    public class MCPTransportService
    {
        private readonly ILogger<MCPTransportService> _logger;
        private readonly MCPServerService _mcpServerService;
        private readonly JsonSerializerOptions _jsonOptions;

        public MCPTransportService(
            ILogger<MCPTransportService> logger,
            MCPServerService mcpServerService)
        {
            _logger = logger;
            _mcpServerService = mcpServerService;
            
            // Configure JSON options for MCP protocol
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Runs the MCP server in stdio transport mode
        /// Reads JSON-RPC messages from stdin and writes responses to stdout
        /// </summary>
        public async Task RunStdioTransportAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting MCP stdio transport mode");

            try
            {
                using var stdin = Console.OpenStandardInput();
                using var stdout = Console.OpenStandardOutput();
                using var reader = new StreamReader(stdin);
                using var writer = new StreamWriter(stdout) { AutoFlush = true };

                string? line;
                while (!cancellationToken.IsCancellationRequested && 
                       (line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        _logger.LogDebug("Received MCP request via stdio: {Request}", line);

                        // Parse the incoming MCP request
                        var request = JsonSerializer.Deserialize<MCPRequest>(line, _jsonOptions);
                        if (request == null)
                        {
                            _logger.LogWarning("Failed to deserialize MCP request: {Request}", line);
                            await SendErrorResponseAsync(writer, null, -32700, "Parse error");
                            continue;
                        }

                        // Process the request using existing MCP server service
                        var response = await _mcpServerService.HandleRequest(request);

                        // Send the response back via stdout
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(responseJson);
                        
                        _logger.LogDebug("Sent MCP response via stdio: {Response}", responseJson);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON parsing error for request: {Request}", line);
                        await SendErrorResponseAsync(writer, null, -32700, "Parse error");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error processing MCP request: {Request}", line);
                        await SendErrorResponseAsync(writer, null, -32603, "Internal error");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in stdio transport");
                throw;
            }
            finally
            {
                _logger.LogInformation("MCP stdio transport stopped");
            }
        }

        /// <summary>
        /// Sends an error response via stdout
        /// </summary>
        private async Task SendErrorResponseAsync(
            StreamWriter writer, 
            object? requestId, 
            int errorCode, 
            string errorMessage)
        {
            try
            {
                var errorResponse = new MCPResponse
                {
                    JsonRpc = "2.0",
                    Id = requestId,
                    Error = new MCPError
                    {
                        Code = errorCode,
                        Message = errorMessage
                    }
                };

                var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                await writer.WriteLineAsync(errorJson);
                
                _logger.LogDebug("Sent error response via stdio: {Response}", errorJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error response");
            }
        }
    }
}
