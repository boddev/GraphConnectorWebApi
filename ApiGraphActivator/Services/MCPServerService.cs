using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.Services
{
    public class MCPServerService
    {
        private readonly ILogger<MCPServerService> _logger;
        private readonly CopilotChatService _copilotChatService;
        private readonly StorageConfigurationService _storageConfigService;
        private readonly DocumentSearchService _documentSearchService;

        public MCPServerService(
            ILogger<MCPServerService> logger,
            CopilotChatService copilotChatService,
            StorageConfigurationService storageConfigService,
            DocumentSearchService documentSearchService)
        {
            _logger = logger;
            _copilotChatService = copilotChatService;
            _storageConfigService = storageConfigService;
            _documentSearchService = documentSearchService;
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
                    tools = new { },
                    resources = new { },
                    prompts = new { }
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
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            company = new { type = "string", description = "Optional: Get last crawl info for specific company" }
                        },
                        required = new string[] { }
                    }
                },
                new
                {
                    name = "get_crawled_companies",
                    description = "Get detailed information about companies that have been successfully crawled including document counts and metrics",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                }
            };

            return Task.FromResult(new MCPResponse
            {
                Id = request.Id,
                Result = new { tools }
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
            
            return toolCall.Name switch
            {
                "search_documents" => await HandleSearchDocuments(request.Id, toolCall.Arguments),
                "get_document_content" => await HandleGetDocumentContent(request.Id, toolCall.Arguments),
                "analyze_document" => await HandleAnalyzeDocument(request.Id, toolCall.Arguments),
                "get_crawl_status" => await HandleGetCrawlStatus(request.Id, toolCall.Arguments),
                "get_last_crawl_info" => await HandleGetLastCrawlInfo(request.Id, toolCall.Arguments),
                "get_crawled_companies" => await HandleGetCrawledCompanies(request.Id, toolCall.Arguments),
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
                
                _logger.LogInformation("Getting last crawl info for company: {Company} using existing services", company ?? "all companies");
                
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
                    var config = await ConfigurationService.LoadCrawledCompaniesAsync();
                    
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
                
                // Use your existing ConfigurationService to get real crawled companies
                var config = await ConfigurationService.LoadCrawledCompaniesAsync();
                
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
