using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;

namespace ApiGraphActivator.Services
{
    public class MCPServerService
    {
        private readonly ILogger<MCPServerService> _logger;
        private readonly CopilotChatService _copilotChatService;
        private readonly StorageConfigurationService _storageConfigService;
        private readonly DocumentSearchService _documentSearchService;
        private readonly ExternalConnectionManagerService _connectionManager;
        private readonly BackgroundTaskQueue _taskQueue;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly bool _isStdioMode;

        // Tools that require Mcp.ReadWrite scope
        private static readonly HashSet<string> WriteTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "start_crawl",
            "manage_connections"
        };

        public MCPServerService(
            ILogger<MCPServerService> logger,
            CopilotChatService copilotChatService,
            StorageConfigurationService storageConfigService,
            DocumentSearchService documentSearchService,
            ExternalConnectionManagerService connectionManager,
            BackgroundTaskQueue taskQueue,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _copilotChatService = copilotChatService;
            _storageConfigService = storageConfigService;
            _documentSearchService = documentSearchService;
            _connectionManager = connectionManager;
            _taskQueue = taskQueue;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            // Stdio mode has no HTTP context at all — detect once at construction
            _isStdioMode = _httpContextAccessor.HttpContext == null;
        }

        private bool HasScope(string requiredScope)
        {
            // In stdio mode (local transport), there is no HTTP context — skip scope checks
            if (_isStdioMode) return true;

            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return false;

            var scopeClaim = user.FindFirst("scp") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/scope");
            if (scopeClaim == null) return false;

            var scopes = scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase);
        }

        private bool CanAccessTool(string toolName)
        {
            if (WriteTools.Contains(toolName))
            {
                return HasScope("Mcp.ReadWrite");
            }
            return HasScope("Mcp.Read") || HasScope("Mcp.ReadWrite");
        }

        private MCPResponse CreateErrorResponse(object? id, int code, string message)
        {
            var response = new MCPResponse { Id = id };
            response.SetError(new MCPError { Code = code, Message = message });
            return response;
        }

        private MCPResponse CreateSuccessResponse(object? id, object result)
        {
            _logger.LogInformation("Creating success response with ID: {Id}", id);
            var response = new MCPResponse { Id = id };
            response.SetResult(result);
            return response;
        }

        public async Task<MCPResponse> HandleRequest(MCPRequest request)
        {
            try
            {
                _logger.LogInformation("Handling MCP request: {Method} with ID: {Id}", request.Method, request.Id);
                
                return request.Method switch
                {
                    "initialize" => await HandleInitialize(request),
                    "tools/list" => await HandleToolsList(request),
                    "tools/call" => await HandleToolCall(request),
                    "resources/list" => await HandleResourcesList(request),
                    "resources/read" => await HandleResourceRead(request),
                    "prompts/list" => await HandlePromptsList(request),
                    _ => CreateErrorResponse(request.Id, -32601, "Method not found")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MCP request: {Method}", request.Method);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        private Task<MCPResponse> HandleInitialize(MCPRequest request)
        {
            _logger.LogInformation("HandleInitialize called with request ID: {Id} (type: {Type})", request.Id, request.Id?.GetType());
            var response = CreateSuccessResponse(request.Id, new
            {
                protocolVersion = "2025-06-18",
                capabilities = new
                {
                    tools = new
                    {
                        listChanged = true
                    },
                    resources = new
                    {
                        subscribe = false,
                        listChanged = false
                    },
                    prompts = new
                    {
                        listChanged = false
                    },
                    logging = new { }
                },
                serverInfo = new
                {
                    name = "SEC Edgar Document Processor",
                    version = "1.0.0"
                }
            });
            _logger.LogInformation("HandleInitialize returning response with ID: {Id} (type: {Type})", response.Id, response.Id?.GetType());
            return Task.FromResult(response);
        }

        private Task<MCPResponse> HandleToolsList(MCPRequest request)
        {
            var tools = new object[]
            {
                new
                {
                    name = "search_documents",
                    description = "Search SEC documents by company, form type, or content",
                    annotations = new
                    {
                        title = "Search SEC Documents",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query" },
                            company = new { type = "string", description = "Company name or ticker" },
                            formType = new { type = "string", description = "SEC form type (10-K, 10-Q, etc.)" },
                            dateRange = new { type = "string", description = "Date range filter" }
                        },
                        required = new[] { "query" }
                    }
                },
                new
                {
                    name = "get_document_content",
                    description = "Retrieve full content of a specific SEC document",
                    annotations = new
                    {
                        title = "Get SEC Document Content",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            documentId = new { type = "string", description = "Document identifier" }
                        },
                        required = new[] { "documentId" }
                    }
                },
                new
                {
                    name = "analyze_document",
                    description = "Analyze SEC document content using AI",
                    annotations = new
                    {
                        title = "Analyze SEC Document",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            documentId = new { type = "string", description = "Document identifier" },
                            analysisType = new { type = "string", description = "Type of analysis to perform" }
                        },
                        required = new[] { "documentId", "analysisType" }
                    }
                },
                new
                {
                    name = "get_crawl_status",
                    description = "Get the current crawl status and progress",
                    annotations = new
                    {
                        title = "Get Crawl Status",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new
                {
                    name = "get_last_crawl_info",
                    description = "Get information about the last crawl including timestamp and results",
                    annotations = new
                    {
                        title = "Get Last Crawl Info",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            company = new { type = "string", description = "Optional: Get last crawl info for specific company" },
                            connectionId = new { type = "string", description = "Optional: Target connection ID (defaults to all connections)" }
                        },
                        required = new string[] { }
                    }
                },
                new
                {
                    name = "get_crawled_companies",
                    description = "Get detailed information about companies that have been successfully crawled including document counts and metrics",
                    annotations = new
                    {
                        title = "Get Crawled Companies",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            connectionId = new { type = "string", description = "Optional: Target connection ID (defaults to all connections)" }
                        },
                        required = new string[] { }
                    }
                },
                new
                {
                    name = "list_companies",
                    description = "List SEC-registered companies available for crawling. Returns company tickers, names, and CIK numbers from the SEC EDGAR database.",
                    annotations = new
                    {
                        title = "List SEC Companies",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            search = new { type = "string", description = "Optional search filter by ticker or company name" },
                            limit = new { type = "integer", description = "Maximum number of results to return (default 50, max 200)" }
                        },
                        required = new string[] { }
                    }
                },
                new
                {
                    name = "start_crawl",
                    description = "Start a crawl operation for specified companies. Queues a background task to extract SEC filings and index them into Microsoft Graph.",
                    annotations = new
                    {
                        title = "Start SEC Document Crawl",
                        readOnlyHint = false,
                        destructiveHint = false,
                        idempotentHint = true
                    },
                    inputSchema = new
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
                                        cik = new { type = "integer", description = "SEC CIK number" },
                                        ticker = new { type = "string", description = "Company ticker symbol" },
                                        title = new { type = "string", description = "Company name" }
                                    },
                                    required = new[] { "cik", "ticker", "title" }
                                }
                            },
                            connectionId = new { type = "string", description = "Target Graph external connection ID" }
                        },
                        required = new[] { "companies", "connectionId" }
                    }
                },
                new
                {
                    name = "manage_connections",
                    description = "Manage Microsoft Graph external connections. List existing connections, create new ones, or delete connections.",
                    annotations = new
                    {
                        title = "Manage Graph Connections",
                        readOnlyHint = false
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", description = "Action to perform: 'list', 'create', or 'delete'" },
                            connectionId = new { type = "string", description = "Connection ID (required for create and delete)" },
                            name = new { type = "string", description = "Connection name (required for create)" },
                            description = new { type = "string", description = "Connection description (required for create)" }
                        },
                        required = new[] { "action" }
                    }
                }
            };

            // Filter tools based on caller's authorization scope
            var hasWriteAccess = HasScope("Mcp.ReadWrite");
            if (!hasWriteAccess)
            {
                tools = tools.Where(t =>
                {
                    var nameProperty = t.GetType().GetProperty("name");
                    var toolName = nameProperty?.GetValue(t)?.ToString();
                    return toolName == null || !WriteTools.Contains(toolName);
                }).ToArray();
            }

            return Task.FromResult(new MCPResponse
            {
                Id = request.Id,
                Result = new { tools }
            });
        }

        private Task<MCPResponse> HandlePromptsList(MCPRequest request)
        {
            // Return empty prompts array as we don't currently support prompts
            var prompts = new object[] { };

            return Task.FromResult(new MCPResponse
            {
                Id = request.Id,
                Result = new { prompts }
            });
        }

        private async Task<MCPResponse> HandleToolCall(MCPRequest request)
        {
            var toolCall = JsonSerializer.Deserialize<ToolCallRequest>(request.Params.ToString());
            
            if (toolCall?.Name == null)
            {
                return new MCPResponse 
                { 
                    Id = request.Id, 
                    Error = new MCPError { Code = -32602, Message = "Invalid tool call parameters" } 
                };
            }

            // Enforce tool-level authorization
            if (!CanAccessTool(toolCall.Name))
            {
                _logger.LogWarning("Unauthorized tool call attempt: {ToolName}", toolCall.Name);
                return CreateErrorResponse(request.Id, -32600, 
                    $"Insufficient permissions to call tool '{toolCall.Name}'. The 'Mcp.ReadWrite' scope is required.");
            }
            
            return toolCall.Name switch
            {
                "search_documents" => await HandleSearchDocuments(request.Id, toolCall.Arguments),
                "get_document_content" => await HandleGetDocumentContent(request.Id, toolCall.Arguments),
                "analyze_document" => await HandleAnalyzeDocument(request.Id, toolCall.Arguments),
                "get_crawl_status" => await HandleGetCrawlStatus(request.Id, toolCall.Arguments),
                "get_last_crawl_info" => await HandleGetLastCrawlInfo(request.Id, toolCall.Arguments),
                "get_crawled_companies" => await HandleGetCrawledCompanies(request.Id, toolCall.Arguments),
                "list_companies" => await HandleListCompanies(request.Id, toolCall.Arguments),
                "start_crawl" => await HandleStartCrawl(request.Id, toolCall.Arguments),
                "manage_connections" => await HandleManageConnections(request.Id, toolCall.Arguments),
                _ => new MCPResponse 
                { 
                    Id = request.Id, 
                    Error = new MCPError { Code = -32602, Message = "Unknown tool" } 
                }
            };
        }

        private async Task<MCPResponse> HandleSearchDocuments(object? requestId, JsonElement arguments)
        {
            try
            {
                var query = arguments.TryGetProperty("query", out var queryProp) ? queryProp.GetString() : null;
                var company = arguments.TryGetProperty("company", out var companyProp) ? companyProp.GetString() : null;
                var formType = arguments.TryGetProperty("formType", out var formTypeProp) ? formTypeProp.GetString() : null;
                var dateRange = arguments.TryGetProperty("dateRange", out var dateRangeProp) ? dateRangeProp.GetString() : null;
                
                _logger.LogInformation("Searching documents with query: {Query}, company: {Company}, formType: {FormType}, dateRange: {DateRange}", 
                    query, company, formType, dateRange);

                // Use the real DocumentSearchService
                var searchRequest = new DocumentSearchRequest
                {
                    Query = query ?? "",
                    Company = company,
                    FormType = formType,
                    DateRange = dateRange
                };

                var searchResult = await _documentSearchService.SearchDocumentsAsync(searchRequest);
                
                if (!searchResult.Success)
                {
                    return new MCPResponse 
                    { 
                        Id = requestId, 
                        Error = new MCPError { Code = -32603, Message = $"Search failed: {searchResult.ErrorMessage}" } 
                    };
                }
                
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
                                text = $"Found {searchResult.TotalResults} documents matching '{query}'" + 
                                       (company != null ? $" for company '{company}'" : "") +
                                       (formType != null ? $" of type '{formType}'" : "") +
                                       (dateRange != null ? $" in date range '{dateRange}'" : "") +
                                       $"\n\nResults: {JsonSerializer.Serialize(searchResult.Documents, new JsonSerializerOptions { WriteIndented = true })}"
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

        private async Task<MCPResponse> HandleGetDocumentContent(object? requestId, JsonElement arguments)
        {
            try
            {
                var documentId = arguments.GetProperty("documentId").GetString();
                
                _logger.LogInformation("Getting document content for ID: {DocumentId}", documentId);
                
                // Use the real DocumentSearchService
                var contentResult = await _documentSearchService.GetDocumentContentAsync(documentId ?? "");
                
                if (!contentResult.Success)
                {
                    return new MCPResponse 
                    { 
                        Id = requestId, 
                        Error = new MCPError { Code = -32603, Message = $"Document retrieval failed: {contentResult.ErrorMessage}" } 
                    };
                }
                
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
                                text = contentResult.Content
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document content");
                return new MCPResponse 
                { 
                    Id = requestId, 
                    Error = new MCPError { Code = -32603, Message = $"Document retrieval error: {ex.Message}" } 
                };
            }
        }

        private async Task<MCPResponse> HandleAnalyzeDocument(object? requestId, JsonElement arguments)
        {
            try
            {
                var documentId = arguments.GetProperty("documentId").GetString();
                var analysisType = arguments.GetProperty("analysisType").GetString();
                
                _logger.LogInformation("Analyzing document {DocumentId} with analysis type: {AnalysisType}", 
                    documentId, analysisType);
                
                // Create a prompt for M365 Copilot based on the analysis type
                var prompt = analysisType?.ToLower() switch
                {
                    "financial" => $"Analyze the financial metrics and key financial information in document {documentId}. Focus on revenue, profit, cash flow, and financial ratios.",
                    "risk" => $"Identify and analyze the risk factors mentioned in document {documentId}. Highlight potential business, operational, and market risks.",
                    "governance" => $"Analyze the corporate governance information in document {documentId}. Focus on board structure, executive compensation, and governance policies.",
                    "summary" => $"Provide a comprehensive summary of the key points and important information in document {documentId}.",
                    _ => $"Analyze document {documentId} and extract the most important information and key insights."
                };
                
                // Use M365 Copilot to analyze the document
                var analysis = await _copilotChatService.GetChatResponseAsync(prompt);
                
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
                                text = analysis
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing document");
                return new MCPResponse 
                { 
                    Id = requestId, 
                    Error = new MCPError { Code = -32603, Message = $"Analysis error: {ex.Message}" } 
                };
            }
        }

        private async Task<MCPResponse> HandleGetCrawlStatus(object? requestId, JsonElement arguments)
        {
            try
            {
                _logger.LogInformation("Getting crawl status using existing service");
                
                // Use your existing storage service to get real crawl status
                var storageService = await _storageConfigService.GetStorageServiceAsync();
                await storageService.InitializeAsync();
                
                var overallMetrics = await storageService.GetCrawlMetricsAsync();
                var unprocessedDocs = await storageService.GetUnprocessedAsync();
                
                var crawlStatus = new
                {
                    isActive = false, // You could track this in a shared state service
                    currentStatus = "idle",
                    lastActivity = overallMetrics.LastProcessedDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    totalDocuments = overallMetrics.TotalDocuments,
                    processedDocuments = overallMetrics.ProcessedDocuments,
                    successfulDocuments = overallMetrics.SuccessfulDocuments,
                    failedDocuments = overallMetrics.FailedDocuments,
                    pendingDocuments = unprocessedDocs.Count,
                    successRate = overallMetrics.SuccessRate,
                    storageType = storageService.GetStorageType(),
                    isHealthy = await storageService.IsHealthyAsync()
                };
                
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
                                text = $"Current Crawl Status:\n\n{JsonSerializer.Serialize(crawlStatus, new JsonSerializerOptions { WriteIndented = true })}"
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting crawl status from storage service");
                return new MCPResponse 
                { 
                    Id = requestId, 
                    Error = new MCPError { Code = -32603, Message = $"Crawl status error: {ex.Message}" } 
                };
            }
        }

        private async Task<MCPResponse> HandleGetLastCrawlInfo(object? requestId, JsonElement arguments)
        {
            try
            {
                var company = arguments.TryGetProperty("company", out var companyProp) ? companyProp.GetString() : null;
                var connectionId = arguments.TryGetProperty("connectionId", out var connProp) ? connProp.GetString() : null;
                
                _logger.LogInformation("Getting last crawl info for company: {Company}, connection: {ConnectionId} using existing services", 
                    company ?? "all companies", connectionId ?? "all connections");
                
                // Use your existing storage service to get real crawl information
                var storageService = await _storageConfigService.GetStorageServiceAsync();
                await storageService.InitializeAsync();
                
                if (!string.IsNullOrEmpty(company))
                {
                    // Get last crawl info for specific company using real metrics
                    try
                    {
                        var companyMetrics = await storageService.GetCrawlMetricsAsync(company);
                        var companyErrors = await storageService.GetProcessingErrorsAsync(company);
                        
                        var companyCrawlInfo = new
                        {
                            company = company,
                            lastProcessedDate = companyMetrics.LastProcessedDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "Never",
                            totalDocuments = companyMetrics.TotalDocuments,
                            processedDocuments = companyMetrics.ProcessedDocuments,
                            successfulDocuments = companyMetrics.SuccessfulDocuments,
                            failedDocuments = companyMetrics.FailedDocuments,
                            successRate = companyMetrics.SuccessRate,
                            errors = companyErrors.Take(5).Select(e => e.ErrorMessage).ToArray() // Limit to recent errors
                        };
                        
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
                                        text = $"Last Crawl Info for {company}:\n\n{JsonSerializer.Serialize(companyCrawlInfo, new JsonSerializerOptions { WriteIndented = true })}"
                                    }
                                }
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not get crawl info for company {Company}: {Error}", company, ex.Message);
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
                                        text = $"No crawl information found for company '{company}'. The company may not have been crawled yet."
                                    }
                                }
                            }
                        };
                    }
                }
                else
                {
                    // Get overall last crawl info using real data
                    var overallMetrics = await storageService.GetCrawlMetricsAsync();
                    var unprocessedDocs = await storageService.GetUnprocessedAsync();
                    var recentErrors = await storageService.GetProcessingErrorsAsync();
                    var config = await ConfigurationService.LoadCrawledCompaniesAsync(connectionId);
                    
                    var lastCrawlInfo = new
                    {
                        lastProcessedDate = overallMetrics.LastProcessedDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "Never",
                        lastCrawlDate = config?.LastCrawlDate.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "Never",
                        totalCompanies = config?.TotalCompanies ?? 0,
                        totalDocuments = overallMetrics.TotalDocuments,
                        processedDocuments = overallMetrics.ProcessedDocuments,
                        successfulDocuments = overallMetrics.SuccessfulDocuments,
                        failedDocuments = overallMetrics.FailedDocuments,
                        pendingDocuments = unprocessedDocs.Count,
                        successRate = overallMetrics.SuccessRate,
                        storageType = storageService.GetStorageType(),
                        isHealthy = await storageService.IsHealthyAsync(),
                        recentErrors = recentErrors.Take(5).Select(e => new 
                        { 
                            company = e.CompanyName, 
                            error = e.ErrorMessage, 
                            errorDate = e.ErrorDate.ToString("yyyy-MM-dd HH:mm:ss") 
                        }).ToArray()
                    };
                    
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
                                    text = $"Overall Last Crawl Information:\n\n{JsonSerializer.Serialize(lastCrawlInfo, new JsonSerializerOptions { WriteIndented = true })}"
                                }
                            }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last crawl info from storage service");
                return new MCPResponse 
                { 
                    Id = requestId, 
                    Error = new MCPError { Code = -32603, Message = $"Last crawl info error: {ex.Message}" } 
                };
            }
        }

        private async Task<MCPResponse> HandleGetCrawledCompanies(object? requestId, JsonElement arguments)
        {
            try
            {
                _logger.LogInformation("Getting detailed crawled companies info using existing configuration service");
                
                var connectionId = arguments.TryGetProperty("connectionId", out var connProp) ? connProp.GetString() : null;
                
                // Use your existing ConfigurationService to get real crawled companies
                var config = await ConfigurationService.LoadCrawledCompaniesAsync(connectionId);
                
                if (config?.Companies == null || !config.Companies.Any())
                {
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
                                    text = "No crawled companies found. Please run a crawl first."
                                }
                            }
                        }
                    };
                }

                // Get detailed document counts and metrics from storage service
                var storageService = await _storageConfigService.GetStorageServiceAsync();
                await storageService.InitializeAsync();
                
                var detailedCompanies = new List<object>();
                
                foreach (var company in config.Companies)
                {
                    try
                    {
                        var companyMetrics = await storageService.GetCrawlMetricsAsync(company.Title);
                        detailedCompanies.Add(new
                        {
                            ticker = company.Ticker,
                            name = company.Title,
                            cik = company.Cik,
                            lastCrawledDate = company.LastCrawledDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                            documentsCount = companyMetrics.TotalDocuments,
                            processedDocuments = companyMetrics.ProcessedDocuments,
                            successfulDocuments = companyMetrics.SuccessfulDocuments,
                            failedDocuments = companyMetrics.FailedDocuments,
                            successRate = companyMetrics.SuccessRate,
                            lastProcessedDate = companyMetrics.LastProcessedDate?.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not get detailed metrics for company {Company}: {Error}", company.Title, ex.Message);
                        detailedCompanies.Add(new
                        {
                            ticker = company.Ticker,
                            name = company.Title,
                            cik = company.Cik,
                            lastCrawledDate = company.LastCrawledDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                            documentsCount = 0,
                            processedDocuments = 0,
                            successfulDocuments = 0,
                            failedDocuments = 0,
                            successRate = 0.0,
                            lastProcessedDate = "Never",
                            error = "Could not retrieve metrics"
                        });
                    }
                }

                var detailedCrawlInfo = new
                {
                    summary = new
                    {
                        lastCrawlDate = config.LastCrawlDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        totalCompanies = config.TotalCompanies,
                        companiesWithData = detailedCompanies.Count
                    },
                    companies = detailedCompanies
                };
                
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
                                text = $"Detailed Crawled Companies Information:\n\n{JsonSerializer.Serialize(detailedCrawlInfo, new JsonSerializerOptions { WriteIndented = true })}"
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed crawled companies from configuration service");
                return new MCPResponse 
                { 
                    Id = requestId, 
                    Error = new MCPError { Code = -32603, Message = $"Detailed crawled companies error: {ex.Message}" } 
                };
            }
        }

        private async Task<MCPResponse> HandleListCompanies(object? requestId, JsonElement arguments)
        {
            try
            {
                var search = arguments.TryGetProperty("search", out var searchProp) ? searchProp.GetString() : null;
                var limit = arguments.TryGetProperty("limit", out var limitProp) ? limitProp.GetInt32() : 50;
                limit = Math.Clamp(limit, 1, 200);

                _logger.LogInformation("Listing companies with search: {Search}, limit: {Limit}", search, limit);

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");

                var response = await httpClient.GetStringAsync("https://www.sec.gov/files/company_tickers.json");
                var tickers = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);

                if (tickers == null)
                {
                    return CreateErrorResponse(requestId, -32603, "Failed to fetch company tickers from SEC");
                }

                var companies = tickers.Values.Select(v =>
                {
                    var cik = v.TryGetProperty("cik_str", out var cikProp) ? cikProp.GetInt32() : 0;
                    var ticker = v.TryGetProperty("ticker", out var tickerProp) ? tickerProp.GetString() : null;
                    var title = v.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                    return new { cik, ticker, title };
                }).Where(c => c.cik > 0);

                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLowerInvariant();
                    companies = companies.Where(c =>
                        (c.ticker?.ToLowerInvariant().Contains(searchLower) == true) ||
                        (c.title?.ToLowerInvariant().Contains(searchLower) == true));
                }

                var results = companies.Take(limit).ToList();

                return CreateSuccessResponse(requestId, new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Found {results.Count} companies" +
                                   (!string.IsNullOrEmpty(search) ? $" matching '{search}'" : "") +
                                   $"\n\n{JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })}"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing companies");
                return CreateErrorResponse(requestId, -32603, $"Error listing companies: {ex.Message}");
            }
        }

        private async Task<MCPResponse> HandleStartCrawl(object? requestId, JsonElement arguments)
        {
            try
            {
                var connectionId = arguments.TryGetProperty("connectionId", out var connProp) ? connProp.GetString() : null;

                if (string.IsNullOrWhiteSpace(connectionId))
                {
                    return CreateErrorResponse(requestId, -32602, "connectionId is required");
                }

                if (!arguments.TryGetProperty("companies", out var companiesElement) ||
                    companiesElement.ValueKind != JsonValueKind.Array ||
                    companiesElement.GetArrayLength() == 0)
                {
                    return CreateErrorResponse(requestId, -32602, "At least one company is required in the 'companies' array");
                }

                var companies = new List<Company>();
                foreach (var item in companiesElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("cik", out var cikProp) ||
                        !item.TryGetProperty("ticker", out var tickerProp) ||
                        !item.TryGetProperty("title", out var titleProp))
                    {
                        return CreateErrorResponse(requestId, -32602, "Each company must have 'cik', 'ticker', and 'title' properties");
                    }

                    companies.Add(new Company
                    {
                        Cik = cikProp.GetInt32(),
                        Ticker = tickerProp.GetString() ?? "",
                        Title = titleProp.GetString() ?? ""
                    });
                }

                _logger.LogInformation("MCP start_crawl: Queueing crawl for {Count} companies to connection {ConnectionId}",
                    companies.Count, connectionId);

                await ConfigurationService.SaveCrawledCompaniesAsync(companies, connectionId);

                await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
                {
                    _logger.LogInformation("MCP background crawl started for {Count} companies in connection {ConnectionId}",
                        companies.Count, connectionId);
                    await ContentService.LoadContentForCompanies(companies, connectionId);
                    _logger.LogInformation("MCP background crawl completed for {Count} companies in connection {ConnectionId}",
                        companies.Count, connectionId);
                });

                return CreateSuccessResponse(requestId, new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Crawl started successfully for {companies.Count} companies to connection '{connectionId}'. " +
                                   $"Companies: {string.Join(", ", companies.Select(c => $"{c.Ticker} ({c.Title})"))}"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting crawl via MCP");
                return CreateErrorResponse(requestId, -32603, $"Error starting crawl: {ex.Message}");
            }
        }

        private async Task<MCPResponse> HandleManageConnections(object? requestId, JsonElement arguments)
        {
            try
            {
                var action = arguments.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;

                if (string.IsNullOrWhiteSpace(action))
                {
                    return CreateErrorResponse(requestId, -32602, "action is required ('list', 'create', or 'delete')");
                }

                switch (action.ToLowerInvariant())
                {
                    case "list":
                    {
                        var connections = await _connectionManager.GetConnectionsAsync();
                        return CreateSuccessResponse(requestId, new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = connections.Any()
                                        ? $"Found {connections.Count} external connections:\n\n{JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true })}"
                                        : "No external connections found."
                                }
                            }
                        });
                    }

                    case "create":
                    {
                        var connId = arguments.TryGetProperty("connectionId", out var cid) ? cid.GetString() : null;
                        var connName = arguments.TryGetProperty("name", out var cn) ? cn.GetString() : null;
                        var connDesc = arguments.TryGetProperty("description", out var cd) ? cd.GetString() : null;

                        if (string.IsNullOrWhiteSpace(connId) || string.IsNullOrWhiteSpace(connName) || string.IsNullOrWhiteSpace(connDesc))
                        {
                            return CreateErrorResponse(requestId, -32602, "connectionId, name, and description are all required for 'create'");
                        }

                        var result = await _connectionManager.CreateConnectionAsync(new CreateExternalConnectionRequest
                        {
                            Id = connId,
                            Name = connName,
                            Description = connDesc
                        });

                        if (result.Success)
                        {
                            return CreateSuccessResponse(requestId, new
                            {
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = $"Connection '{connId}' created successfully.\n\n{JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true })}"
                                    }
                                }
                            });
                        }
                        return CreateErrorResponse(requestId, -32603, $"Failed to create connection: {result.ErrorMessage}");
                    }

                    case "delete":
                    {
                        var deleteId = arguments.TryGetProperty("connectionId", out var did) ? did.GetString() : null;

                        if (string.IsNullOrWhiteSpace(deleteId))
                        {
                            return CreateErrorResponse(requestId, -32602, "connectionId is required for 'delete'");
                        }

                        var result = await _connectionManager.DeleteConnectionAsync(deleteId);
                        if (result.Success)
                        {
                            return CreateSuccessResponse(requestId, new
                            {
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = $"Connection '{deleteId}' deleted successfully."
                                    }
                                }
                            });
                        }
                        return CreateErrorResponse(requestId, -32603, $"Failed to delete connection: {result.ErrorMessage}");
                    }

                    default:
                        return CreateErrorResponse(requestId, -32602, $"Unknown action '{action}'. Use 'list', 'create', or 'delete'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing connections via MCP");
                return CreateErrorResponse(requestId, -32603, $"Error managing connections: {ex.Message}");
            }
        }

        private Task<MCPResponse> HandleResourcesList(MCPRequest request)
        {
            return Task.FromResult(new MCPResponse
            {
                Id = request.Id,
                Result = new { resources = new object[] { } }
            });
        }

        private Task<MCPResponse> HandleResourceRead(MCPRequest request)
        {
            return Task.FromResult(new MCPResponse
            {
                Id = request.Id,
                Result = new { contents = new object[] { } }
            });
        }
    }
}
