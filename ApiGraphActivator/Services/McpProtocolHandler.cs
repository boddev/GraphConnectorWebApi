using System.Text.Json;
using ApiGraphActivator.Models.Mcp;

namespace ApiGraphActivator.Services;

/// <summary>
/// MCP Protocol Handler responsible for processing MCP messages and managing protocol state
/// </summary>
public class McpProtocolHandler
{
    private readonly ILogger<McpProtocolHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isInitialized = false;
    private McpServerInfo? _serverInfo;
    private McpServerCapabilities? _serverCapabilities;

    public McpProtocolHandler(ILogger<McpProtocolHandler> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        InitializeServerInfo();
    }

    /// <summary>
    /// Initialize server information and capabilities
    /// </summary>
    private void InitializeServerInfo()
    {
        _serverInfo = new McpServerInfo
        {
            Name = "SEC Edgar Graph Connector",
            Version = "1.0.0",
            ProtocolVersion = "2024-11-05"
        };

        _serverCapabilities = new McpServerCapabilities
        {
            Resources = new McpResourceCapability
            {
                Subscribe = false,
                ListChanged = false
            },
            Tools = new McpToolCapability
            {
                ListChanged = false
            },
            Prompts = new McpPromptCapability
            {
                ListChanged = false
            }
        };
    }

    /// <summary>
    /// Process incoming MCP message and return response
    /// </summary>
    public async Task<string?> ProcessMessageAsync(string messageJson)
    {
        try
        {
            _logger.LogDebug("Processing MCP message: {Message}", messageJson);

            // Try to parse as different message types
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;

            // Check if it's a valid JSON-RPC 2.0 message
            if (!root.TryGetProperty("jsonrpc", out var jsonrpcProperty) || 
                jsonrpcProperty.GetString() != "2.0")
            {
                return CreateErrorResponse(null, McpErrorCodes.InvalidRequest, "Invalid JSON-RPC 2.0 format");
            }

            // Determine message type
            if (root.TryGetProperty("method", out var methodProperty))
            {
                var method = methodProperty.GetString();
                var hasId = root.TryGetProperty("id", out var idProperty);

                if (hasId)
                {
                    // Request message
                    var request = JsonSerializer.Deserialize<McpRequest>(messageJson, _jsonOptions);
                    return await ProcessRequestAsync(request!);
                }
                else
                {
                    // Notification message
                    var notification = JsonSerializer.Deserialize<McpNotification>(messageJson, _jsonOptions);
                    await ProcessNotificationAsync(notification!);
                    return null; // Notifications don't get responses
                }
            }
            else
            {
                _logger.LogWarning("Received message without method property");
                return CreateErrorResponse(null, McpErrorCodes.InvalidRequest, "Missing method property");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON message");
            return CreateErrorResponse(null, McpErrorCodes.ParseError, "Invalid JSON format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing MCP message");
            return CreateErrorResponse(null, McpErrorCodes.InternalError, "Internal server error");
        }
    }

    /// <summary>
    /// Process MCP request and return response
    /// </summary>
    private async Task<string> ProcessRequestAsync(McpRequest request)
    {
        try
        {
            _logger.LogDebug("Processing MCP request: {Method}", request.Method);

            return request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "resources/list" => await HandleListResourcesAsync(request),
                "resources/read" => await HandleReadResourceAsync(request),
                "tools/list" => await HandleListToolsAsync(request),
                "tools/call" => await HandleCallToolAsync(request),
                "prompts/list" => await HandleListPromptsAsync(request),
                _ => CreateErrorResponse(request.Id, McpErrorCodes.MethodNotFound, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request {Method}", request.Method);
            return CreateErrorResponse(request.Id, McpErrorCodes.InternalError, "Internal server error");
        }
    }

    /// <summary>
    /// Process MCP notification
    /// </summary>
    private async Task ProcessNotificationAsync(McpNotification notification)
    {
        _logger.LogDebug("Processing MCP notification: {Method}", notification.Method);

        switch (notification.Method)
        {
            case "initialized":
                await HandleInitializedNotificationAsync(notification);
                break;
            case "notifications/progress":
                await HandleProgressNotificationAsync(notification);
                break;
            default:
                _logger.LogWarning("Unknown notification method: {Method}", notification.Method);
                break;
        }
    }

    /// <summary>
    /// Handle initialize request
    /// </summary>
    private async Task<string> HandleInitializeAsync(McpRequest request)
    {
        try
        {
            var initParams = JsonSerializer.Deserialize<McpInitializeParams>(
                request.Params?.ToString() ?? "{}", _jsonOptions);

            _logger.LogInformation("Client initialized with protocol version: {Version}", 
                initParams?.ProtocolVersion);

            var result = new McpInitializeResult
            {
                ProtocolVersion = _serverInfo!.ProtocolVersion,
                Capabilities = _serverCapabilities,
                ServerInfo = _serverInfo
            };

            return CreateSuccessResponse(request.Id, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling initialize request");
            return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Invalid initialize parameters");
        }
    }

    /// <summary>
    /// Handle initialized notification
    /// </summary>
    private async Task HandleInitializedNotificationAsync(McpNotification notification)
    {
        _isInitialized = true;
        _logger.LogInformation("MCP client initialization completed");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handle list resources request
    /// </summary>
    private async Task<string> HandleListResourcesAsync(McpRequest request)
    {
        var resources = new List<McpResource>
        {
            new McpResource
            {
                Uri = "sec://filings/list",
                Name = "SEC Filings List",
                Description = "List of available SEC filings in the system",
                MimeType = "application/json"
            },
            new McpResource
            {
                Uri = "sec://companies/list",
                Name = "Company List",
                Description = "List of tracked companies",
                MimeType = "application/json"
            },
            new McpResource
            {
                Uri = "sec://crawl/status",
                Name = "Crawl Status",
                Description = "Current crawling status and metrics",
                MimeType = "application/json"
            }
        };

        var result = new McpListResourcesResult { Resources = resources };
        return CreateSuccessResponse(request.Id, result);
    }

    /// <summary>
    /// Handle read resource request
    /// </summary>
    private async Task<string> HandleReadResourceAsync(McpRequest request)
    {
        try
        {
            var readParams = JsonSerializer.Deserialize<McpReadResourceParams>(
                request.Params?.ToString() ?? "{}", _jsonOptions);

            if (string.IsNullOrEmpty(readParams?.Uri))
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing resource URI");
            }

            // Handle different resource types
            var content = readParams.Uri switch
            {
                "sec://filings/list" => await GetFilingsListAsync(),
                "sec://companies/list" => await GetCompaniesListAsync(),
                "sec://crawl/status" => await GetCrawlStatusAsync(),
                _ => throw new NotSupportedException($"Resource not found: {readParams.Uri}")
            };

            var result = new McpReadResourceResult
            {
                Contents = new List<McpResourceContent>
                {
                    new McpResourceContent
                    {
                        Uri = readParams.Uri,
                        MimeType = "application/json",
                        Text = content
                    }
                }
            };

            return CreateSuccessResponse(request.Id, result);
        }
        catch (NotSupportedException ex)
        {
            return CreateErrorResponse(request.Id, McpErrorCodes.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource");
            return CreateErrorResponse(request.Id, McpErrorCodes.InternalError, "Error reading resource");
        }
    }

    /// <summary>
    /// Handle list tools request
    /// </summary>
    private async Task<string> HandleListToolsAsync(McpRequest request)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "start_crawl",
                Description = "Start crawling SEC filings for specified companies",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        companies = new
                        {
                            type = "array",
                            description = "List of companies to crawl",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    cik = new { type = "string" },
                                    ticker = new { type = "string" },
                                    title = new { type = "string" }
                                }
                            }
                        }
                    },
                    required = new[] { "companies" }
                }
            },
            new McpTool
            {
                Name = "get_crawl_metrics",
                Description = "Get crawling metrics and status",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        companyName = new
                        {
                            type = "string",
                            description = "Optional company name to filter metrics"
                        }
                    }
                }
            }
        };

        var result = new McpListToolsResult { Tools = tools };
        return CreateSuccessResponse(request.Id, result);
    }

    /// <summary>
    /// Handle call tool request
    /// </summary>
    private async Task<string> HandleCallToolAsync(McpRequest request)
    {
        try
        {
            var callParams = JsonSerializer.Deserialize<McpCallToolParams>(
                request.Params?.ToString() ?? "{}", _jsonOptions);

            if (string.IsNullOrEmpty(callParams?.Name))
            {
                return CreateErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing tool name");
            }

            var content = callParams.Name switch
            {
                "start_crawl" => await HandleStartCrawlTool(callParams.Arguments),
                "get_crawl_metrics" => await HandleGetCrawlMetricsTool(callParams.Arguments),
                _ => throw new NotSupportedException($"Tool not found: {callParams.Name}")
            };

            var result = new McpCallToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = content
                    }
                },
                IsError = false
            };

            return CreateSuccessResponse(request.Id, result);
        }
        catch (NotSupportedException ex)
        {
            return CreateErrorResponse(request.Id, McpErrorCodes.MethodNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool");
            return CreateErrorResponse(request.Id, McpErrorCodes.InternalError, "Error executing tool");
        }
    }

    /// <summary>
    /// Handle list prompts request
    /// </summary>
    private async Task<string> HandleListPromptsAsync(McpRequest request)
    {
        var prompts = new List<McpPrompt>
        {
            new McpPrompt
            {
                Name = "analyze_filing",
                Description = "Analyze a specific SEC filing",
                Arguments = new List<McpPromptArgument>
                {
                    new McpPromptArgument
                    {
                        Name = "company",
                        Description = "Company name or ticker",
                        Required = true
                    },
                    new McpPromptArgument
                    {
                        Name = "formType",
                        Description = "SEC form type (10-K, 10-Q, 8-K)",
                        Required = false
                    }
                }
            }
        };

        var result = new McpListPromptsResult { Prompts = prompts };
        return CreateSuccessResponse(request.Id, result);
    }

    /// <summary>
    /// Handle progress notification
    /// </summary>
    private async Task HandleProgressNotificationAsync(McpNotification notification)
    {
        _logger.LogDebug("Received progress notification");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Create success response
    /// </summary>
    private string CreateSuccessResponse(object? id, object result)
    {
        var response = new McpResponse
        {
            Id = id,
            Result = result
        };

        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    /// <summary>
    /// Create error response
    /// </summary>
    private string CreateErrorResponse(object? id, int errorCode, string message)
    {
        var response = new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = errorCode,
                Message = message
            }
        };

        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    // Helper methods for resource data
    private async Task<string> GetFilingsListAsync()
    {
        // Return sample filings data - in real implementation, this would query the storage service
        var filings = new
        {
            totalCount = 100,
            filings = new[]
            {
                new { company = "Apple Inc.", form = "10-K", date = "2024-01-01", url = "https://example.com" },
                new { company = "Microsoft Corp.", form = "10-Q", date = "2024-01-15", url = "https://example.com" }
            }
        };

        return JsonSerializer.Serialize(filings, _jsonOptions);
    }

    private async Task<string> GetCompaniesListAsync()
    {
        // Return sample companies data
        var companies = new
        {
            totalCount = 50,
            companies = new[]
            {
                new { cik = "320193", ticker = "AAPL", name = "Apple Inc." },
                new { cik = "789019", ticker = "MSFT", name = "Microsoft Corp." }
            }
        };

        return JsonSerializer.Serialize(companies, _jsonOptions);
    }

    private async Task<string> GetCrawlStatusAsync()
    {
        // Return sample crawl status
        var status = new
        {
            isRunning = false,
            totalDocuments = 1000,
            processedDocuments = 850,
            successfulDocuments = 820,
            failedDocuments = 30,
            lastRunDate = DateTime.UtcNow.AddHours(-2)
        };

        return JsonSerializer.Serialize(status, _jsonOptions);
    }

    private async Task<string> HandleStartCrawlTool(Dictionary<string, object>? arguments)
    {
        // In real implementation, this would trigger the crawl process
        return JsonSerializer.Serialize(new
        {
            status = "started",
            message = "Crawl process initiated successfully",
            timestamp = DateTime.UtcNow
        }, _jsonOptions);
    }

    private async Task<string> HandleGetCrawlMetricsTool(Dictionary<string, object>? arguments)
    {
        // Return sample metrics
        var metrics = new
        {
            totalDocuments = 1000,
            processedDocuments = 850,
            successRate = 0.97,
            averageProcessingTime = "2.5 seconds",
            lastUpdate = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(metrics, _jsonOptions);
    }

    /// <summary>
    /// Get current protocol state
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Get server information
    /// </summary>
    public McpServerInfo? ServerInfo => _serverInfo;

    /// <summary>
    /// Get server capabilities
    /// </summary>
    public McpServerCapabilities? ServerCapabilities => _serverCapabilities;
}