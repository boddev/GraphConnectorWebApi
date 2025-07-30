using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ApiGraphActivator;
using System.Text.Json;
using ApiGraphActivator.McpTools;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Add Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Explicitly set instrumentation key if needed
    // options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddApplicationInsights();
    
    // Set minimum log level for Application Insights
    loggingBuilder.AddFilter<ApplicationInsightsLoggerProvider>("ApiGraphActivator.Services", LogLevel.Trace);
});

// Register your services as non-static when possible
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<StorageConfigurationService>();
builder.Services.AddSingleton<BackgroundTaskQueue>(sp => new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddHostedService<SchedulerService>();

// Register MCP document search services
builder.Services.AddScoped<DocumentSearchService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<DocumentSearchService>>();
    var storageConfigService = serviceProvider.GetRequiredService<StorageConfigurationService>();
    var storageService = storageConfigService.GetStorageServiceAsync().GetAwaiter().GetResult();
    return new DocumentSearchService(logger, storageService);
});
builder.Services.AddScoped<CompanySearchTool>();
builder.Services.AddScoped<FormFilterTool>();
builder.Services.AddScoped<ContentSearchTool>();

// Register MCP prompt template services
builder.Services.AddSingleton<PromptTemplateService>();
builder.Services.AddScoped<McpPromptsService>();
builder.Services.AddScoped<PromptAnalysisService>();

// Register OpenAI service only if API key is available
var openAIKey = Environment.GetEnvironmentVariable("OpenAIKey");
if (!string.IsNullOrEmpty(openAIKey))
{
    builder.Services.AddScoped<OpenAIService>();
}

// For static services that need logging
builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowReactApp");

// Create a factory-based logger that's appropriate for static classes
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var staticServiceLogger = loggerFactory.CreateLogger("ApiGraphActivator.Services.StaticServices");

// Pass the correctly created logger to your static services
EdgarService.InitializeLogger(staticServiceLogger);
ConnectionService.InitializeLogger(staticServiceLogger);
PdfProcessingService.InitializeLogger(staticServiceLogger);

// Initialize storage service for EdgarService
var storageConfigService = app.Services.GetRequiredService<StorageConfigurationService>();
try
{
    var storageService = await storageConfigService.GetStorageServiceAsync();
    await EdgarService.InitializeStorageServiceAsync(storageService);
    staticServiceLogger.LogInformation("Storage service initialized for EdgarService");
}
catch (Exception ex)
{
    staticServiceLogger.LogWarning("Failed to initialize storage service: {Message}", ex.Message);
}

// Log a test message to verify Application Insights is working
staticServiceLogger.LogInformation("Application started and Application Insights is configured.");

app.MapGet("/", () => "Hello World!")
    .WithName("GetHelloWorld")
    .WithOpenApi();

app.MapPost("/grantPermissions", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var tenantId = await reader.ReadToEndAsync();

    // Log the tenant ID
    staticServiceLogger.LogInformation("Received tenant ID: {TenantId}", tenantId);

    // Store the tenant ID in a text file
    await File.WriteAllTextAsync("tenantid.txt", tenantId);

    // Redirect the user to the specified URL
    var clientId = "your-client-id"; // Replace with your actual client ID
    var redirectUrl = $"https://login.microsoftonline.com/organizations/adminconsent?client_id={clientId}";
    context.Response.Redirect(redirectUrl);
})
.WithName("StoreTenantId")
.WithOpenApi();

app.MapPost("/provisionconnection", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var tenantId = await reader.ReadToEndAsync();

    // Log the tenant ID
    staticServiceLogger.LogInformation("Provisioning connection for tenant ID: {TenantId}", tenantId);

    // Call the ProvisionConnection method with the tenant ID
    try{
        await ConnectionService.ProvisionConnection();
        await context.Response.WriteAsync("Connection provisioned successfully.");
    } catch (Exception ex) {
        // Log the exception message
        staticServiceLogger.LogError("Error provisioning connection: {Message}", ex.Message);
        await context.Response.WriteAsync($"Error provisioning connection: {ex.Message}");
    }
})
.WithName("provisionconnection")
.WithOpenApi();

app.MapPost("/loadcontent", async (HttpContext context, BackgroundTaskQueue taskQueue) =>
{
    try
    {
        staticServiceLogger.LogInformation("Received loadcontent request");
        
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        
        staticServiceLogger.LogInformation("Request body: {RequestBody}", requestBody);
        
        // Try to parse as JSON first (new format), then fall back to old format
        CrawlRequest? crawlRequest = null;
        try
        {
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            crawlRequest = System.Text.Json.JsonSerializer.Deserialize<CrawlRequest>(requestBody, jsonOptions);
            staticServiceLogger.LogInformation("Successfully parsed crawl request with {CompanyCount} companies", 
                crawlRequest?.Companies?.Count ?? 0);
        }
        catch (Exception ex)
        {
            // Fall back to old format (just tenant ID)
            staticServiceLogger.LogInformation("Failed to parse as JSON, treating as tenant ID: {TenantId}. Error: {Error}", 
                requestBody, ex.Message);
        }

        if (crawlRequest?.Companies?.Any() == true)
        {
            staticServiceLogger.LogInformation("Loading content for {CompanyCount} companies", crawlRequest.Companies.Count);
            
            // Save companies to config file for persistence
            await ConfigurationService.SaveCrawledCompaniesAsync(crawlRequest.Companies);
            
            staticServiceLogger.LogInformation("Queueing background task for crawl");
            
            // Queue the long-running task with selected companies
            await taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                staticServiceLogger.LogInformation("Background task started for {CompanyCount} companies", crawlRequest.Companies.Count);
                await ContentService.LoadContentForCompanies(crawlRequest.Companies);
                staticServiceLogger.LogInformation("Background task completed for {CompanyCount} companies", crawlRequest.Companies.Count);
            });
            
            staticServiceLogger.LogInformation("Background task queued successfully");
        }
        else
        {
            // Queue the original long-running task
            await taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ContentService.LoadContent();
            });
        }

        // Return a response immediately
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        await context.Response.WriteAsync("Crawl started successfully");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error starting crawl: {Message}", ex.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error starting crawl: {ex.Message}");
    }
})
.WithName("loadcontent")
.WithOpenApi();

app.MapPost("/recrawl-all", async (HttpContext context, BackgroundTaskQueue taskQueue) =>
{
    try
    {
        staticServiceLogger.LogInformation("Received recrawl-all request");
        
        // Load previously crawled companies
        var config = await ConfigurationService.LoadCrawledCompaniesAsync();
        
        if (config?.Companies?.Any() != true)
        {
            staticServiceLogger.LogWarning("No previously crawled companies found");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("No previously crawled companies found. Please crawl companies first.");
            return;
        }

        staticServiceLogger.LogInformation("Starting recrawl for {CompanyCount} previously crawled companies", config.Companies.Count);
        
        // Queue the background task with previously crawled companies
        await taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            staticServiceLogger.LogInformation("Background recrawl task started for {CompanyCount} companies", config.Companies.Count);
            await ContentService.LoadContentForCompanies(config.Companies);
            staticServiceLogger.LogInformation("Background recrawl task completed for {CompanyCount} companies", config.Companies.Count);
        });
        
        staticServiceLogger.LogInformation("Background recrawl task queued successfully");
        
        // Return a response immediately
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        await context.Response.WriteAsync($"Recrawl started successfully for {config.Companies.Count} companies");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error starting recrawl: {Message}", ex.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error starting recrawl: {ex.Message}");
    }
})
.WithName("RecrawlAll")
.WithOpenApi();

app.MapGet("/companies", async (HttpContext context) =>
{
    try
    {
        staticServiceLogger.LogInformation("Fetching company tickers from SEC");
        
        // Use EdgarService's HttpClient which has proper User-Agent
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
        
        var response = await httpClient.GetStringAsync("https://www.sec.gov/files/company_tickers.json");
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(response);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching company tickers: {Message}", ex.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error fetching company tickers: {ex.Message}");
    }
})
.WithName("GetCompanies")
.WithOpenApi();

app.MapGet("/crawled-companies", async (HttpContext context) =>
{
    try
    {
        staticServiceLogger.LogInformation("Fetching previously crawled companies");
        var config = await ConfigurationService.LoadCrawledCompaniesAsync();
        
        if (config == null)
        {
            var emptyResult = new { 
                lastCrawlDate = (DateTime?)null,
                companies = new List<Company>(),
                totalCompanies = 0
            };
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(emptyResult));
            return;
        }

        var result = new {
            lastCrawlDate = config.LastCrawlDate,
            companies = config.Companies,
            totalCompanies = config.TotalCompanies
        };
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result));
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching crawled companies: {Message}", ex.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error fetching crawled companies: {ex.Message}");
    }
})
.WithName("GetCrawledCompanies")
.WithOpenApi();

// Storage Configuration Endpoints
app.MapGet("/storage-config", async (StorageConfigurationService storageConfigService) =>
{
    var config = await storageConfigService.GetConfigurationAsync();
    return Results.Ok(config);
})
.WithName("GetStorageConfig")
.WithOpenApi();

app.MapPost("/storage-config", async (StorageConfiguration config, StorageConfigurationService storageConfigService) =>
{
    try
    {
        await storageConfigService.SaveConfigurationAsync(config);
        return Results.Ok(new { message = "Storage configuration saved successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = $"Failed to save configuration: {ex.Message}" });
    }
})
.WithName("SaveStorageConfig")
.WithOpenApi();

app.MapPost("/storage-config/test", async (StorageConfiguration config, StorageConfigurationService storageConfigService) =>
{
    try
    {
        bool isHealthy = await storageConfigService.TestConnectionAsync(config);
        return Results.Ok(new { 
            healthy = isHealthy, 
            message = isHealthy ? "Connection successful" : "Connection failed" 
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            healthy = false, 
            message = $"Connection test failed: {ex.Message}" 
        });
    }
})
.WithName("TestStorageConfig")
.WithOpenApi();

// Crawl Tracking and Metrics Endpoints
app.MapGet("/crawl-metrics", async (StorageConfigurationService storageConfigService) =>
{
    try
    {
        var overallMetrics = await storageConfigService.GetOverallMetricsAsync();
        return Results.Ok(overallMetrics);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching overall crawl metrics: {Message}", ex.Message);
        return Results.Problem($"Error fetching crawl metrics: {ex.Message}");
    }
})
.WithName("GetCrawlMetrics")
.WithOpenApi();

app.MapGet("/crawl-metrics/{companyName}", async (string companyName, StorageConfigurationService storageConfigService) =>
{
    try
    {
        var storageService = await storageConfigService.GetStorageServiceAsync();
        await storageService.InitializeAsync();
        var companyMetrics = await storageService.GetCrawlMetricsAsync(companyName);
        return Results.Ok(companyMetrics);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching crawl metrics for company {Company}: {Message}", companyName, ex.Message);
        return Results.Problem($"Error fetching crawl metrics for {companyName}: {ex.Message}");
    }
})
.WithName("GetCompanyCrawlMetrics")
.WithOpenApi();

app.MapGet("/crawl-errors", async (StorageConfigurationService storageConfigService, string? company = null) =>
{
    try
    {
        var storageService = await storageConfigService.GetStorageServiceAsync();
        await storageService.InitializeAsync();
        var errors = await storageService.GetProcessingErrorsAsync(company);
        return Results.Ok(errors);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching processing errors: {Message}", ex.Message);
        return Results.Problem($"Error fetching processing errors: {ex.Message}");
    }
})
.WithName("GetProcessingErrors")
.WithOpenApi();

app.MapGet("/crawl-status", async (StorageConfigurationService storageConfigService) =>
{
    try
    {
        var storageService = await storageConfigService.GetStorageServiceAsync();
        await storageService.InitializeAsync();
        
        var overallMetrics = await storageService.GetCrawlMetricsAsync();
        var unprocessedDocs = await storageService.GetUnprocessedAsync();
        
        var status = new
        {
            TotalDocuments = overallMetrics.TotalDocuments,
            ProcessedDocuments = overallMetrics.ProcessedDocuments,
            SuccessfulDocuments = overallMetrics.SuccessfulDocuments,
            FailedDocuments = overallMetrics.FailedDocuments,
            PendingDocuments = unprocessedDocs.Count,
            SuccessRate = overallMetrics.SuccessRate,
            LastProcessedDate = overallMetrics.LastProcessedDate,
            StorageType = storageService.GetStorageType(),
            IsHealthy = await storageService.IsHealthyAsync()
        };
        
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error fetching crawl status: {Message}", ex.Message);
        return Results.Problem($"Error fetching crawl status: {ex.Message}");
    }
})
.WithName("GetCrawlStatus")
.WithOpenApi();

// Data Collection Configuration endpoints
app.MapGet("/data-collection-config", async (HttpContext context) =>
{
    try
    {
        var config = await DataCollectionConfigurationService.LoadConfigurationAsync();
        return Results.Ok(config);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error retrieving data collection config: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("GetDataCollectionConfig")
.WithOpenApi();

app.MapPost("/data-collection-config", async (HttpContext context) =>
{
    try
    {
        var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var config = JsonSerializer.Deserialize<DataCollectionConfiguration>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        if (config != null)
        {
            await DataCollectionConfigurationService.SaveConfigurationAsync(config);
            staticServiceLogger.LogInformation("Data collection config updated: {Years} years of data", config.YearsOfData);
            return Results.Ok(config);
        }
        
        return Results.BadRequest("Invalid configuration data");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error saving data collection config: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("SaveDataCollectionConfig")
.WithOpenApi();

// Yearly metrics endpoints
app.MapGet("/crawl-metrics/yearly", async (StorageConfigurationService storageConfigService) =>
{
    try
    {
        var storageService = await storageConfigService.GetStorageServiceAsync();
        var yearlyMetrics = await storageService.GetYearlyMetricsAsync();
        return Results.Ok(yearlyMetrics);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error retrieving yearly metrics: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("GetYearlyMetrics")
.WithOpenApi();

app.MapGet("/crawl-metrics/yearly/{companyName}", async (string companyName, StorageConfigurationService storageConfigService) =>
{
    try
    {
        var storageService = await storageConfigService.GetStorageServiceAsync();
        var yearlyMetrics = await storageService.GetCompanyYearlyMetricsAsync(companyName);
        return Results.Ok(yearlyMetrics);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error retrieving yearly metrics for company {Company}: {Error}", companyName, ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("GetCompanyYearlyMetrics")
.WithOpenApi();

// Scheduler Configuration Endpoints
app.MapGet("/scheduler-config", async (HttpContext context) =>
{
    try
    {
        staticServiceLogger.LogInformation("Received request to get scheduler config");
        
        // Create a temporary instance to access the config file
        var tempScheduler = new SchedulerService(
            context.RequestServices.GetRequiredService<ILogger<SchedulerService>>(),
            context.RequestServices.GetRequiredService<BackgroundTaskQueue>()
        );
        
        var config = await tempScheduler.LoadScheduleConfigAsync();
        return Results.Ok(config);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting scheduler config: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("GetSchedulerConfig")
.WithOpenApi();

app.MapPost("/scheduler-config", async (HttpContext context) =>
{
    try
    {
        staticServiceLogger.LogInformation("Received request to save scheduler config");
        
        var config = await context.Request.ReadFromJsonAsync<ScheduleConfig>();
        if (config == null)
        {
            return Results.BadRequest("Invalid configuration data");
        }

        // Create a temporary instance to save the config
        var tempScheduler = new SchedulerService(
            context.RequestServices.GetRequiredService<ILogger<SchedulerService>>(),
            context.RequestServices.GetRequiredService<BackgroundTaskQueue>()
        );
        
        // Recalculate next run time when config is updated
        if (config.Enabled)
        {
            config.NextScheduledRun = null; // Will be recalculated by the service
        }
        
        await tempScheduler.SaveScheduleConfigAsync(config);
        
        staticServiceLogger.LogInformation("Scheduler config saved successfully. Enabled: {Enabled}, Frequency: {Frequency}", 
            config.Enabled, config.Frequency);
        
        return Results.Ok(new { message = "Scheduler configuration saved successfully", config });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error saving scheduler config: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("SaveSchedulerConfig")
.WithOpenApi();

// MCP Document Search Tool Endpoints
app.MapPost("/mcp/tools/company-search", async (CompanySearchParameters parameters, CompanySearchTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing company search tool: {Message}", ex.Message);
        return Results.Problem($"Company search failed: {ex.Message}");
    }
})
.WithName("McpCompanySearch")
.WithOpenApi()
.WithSummary("MCP Tool: Search documents by company name")
.WithDescription("Search SEC filing documents by company name with optional filtering by form types and date ranges");

app.MapPost("/mcp/tools/form-filter", async (FormFilterParameters parameters, FormFilterTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing form filter tool: {Message}", ex.Message);
        return Results.Problem($"Form filter failed: {ex.Message}");
    }
})
.WithName("McpFormFilter")
.WithOpenApi()
.WithSummary("MCP Tool: Filter documents by form type and date")
.WithDescription("Filter SEC filing documents by form type and date range with optional company filtering");

app.MapPost("/mcp/tools/content-search", async (ContentSearchParameters parameters, ContentSearchTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing content search tool: {Message}", ex.Message);
        return Results.Problem($"Content search failed: {ex.Message}");
    }
})
.WithName("McpContentSearch")
.WithOpenApi()
.WithSummary("MCP Tool: Search document content")
.WithDescription("Perform full-text search within SEC filing document content with highlighting and relevance scoring");

// MCP Tools Discovery Endpoint
app.MapGet("/mcp/tools", (CompanySearchTool companyTool, FormFilterTool formTool, ContentSearchTool contentTool) =>
{
    var tools = new[]
    {
        new
        {
            name = companyTool.Name,
            description = companyTool.Description,
            inputSchema = companyTool.InputSchema,
            endpoint = "/mcp/tools/company-search"
        },
        new
        {
            name = formTool.Name,
            description = formTool.Description,
            inputSchema = formTool.InputSchema,
            endpoint = "/mcp/tools/form-filter"
        },
        new
        {
            name = contentTool.Name,
            description = contentTool.Description,
            inputSchema = contentTool.InputSchema,
            endpoint = "/mcp/tools/content-search"
        }
    };

    return Results.Ok(new { tools });
})
.WithName("McpToolsDiscovery")
.WithOpenApi()
.WithSummary("MCP Tools Discovery")
.WithDescription("List all available MCP document search tools with their schemas and endpoints");

// MCP Prompts Endpoints (per MCP specification)
app.MapGet("/mcp/prompts/list", async (McpPromptsService promptsService, string? cursor) =>
{
    try
    {
        var request = cursor != null ? new McpPromptsListRequest { Cursor = cursor } : null;
        var result = await promptsService.GetPromptsAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error listing MCP prompts: {Message}", ex.Message);
        return Results.Problem($"Failed to list prompts: {ex.Message}");
    }
})
.WithName("McpPromptsList")
.WithOpenApi()
.WithSummary("MCP Prompts List")
.WithDescription("List all available prompt templates as per MCP specification");

app.MapPost("/mcp/prompts/get", async (McpPromptGetRequest request, McpPromptsService promptsService) =>
{
    try
    {
        var result = await promptsService.GetPromptAsync(request.Name, request.Arguments);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        staticServiceLogger.LogWarning("Invalid prompt request: {Message}", ex.Message);
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting MCP prompt: {Message}", ex.Message);
        return Results.Problem($"Failed to get prompt: {ex.Message}");
    }
})
.WithName("McpPromptsGet")
.WithOpenApi()
.WithSummary("MCP Prompts Get")
.WithDescription("Get a specific prompt template with optional parameter rendering");

// Additional prompt template management endpoints
app.MapGet("/mcp/prompts/templates", (PromptTemplateService templateService, string? category) =>
{
    try
    {
        var templates = string.IsNullOrEmpty(category) 
            ? templateService.GetAllTemplates()
            : templateService.GetTemplatesByCategory(category);

        return Results.Ok(new { templates = templates.ToList() });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting prompt templates: {Message}", ex.Message);
        return Results.Problem($"Failed to get templates: {ex.Message}");
    }
})
.WithName("PromptsTemplatesList")
.WithOpenApi()
.WithSummary("Get Prompt Templates")
.WithDescription("Get all prompt templates or filter by category");

app.MapPost("/mcp/prompts/render", async (RenderPromptRequest request, PromptTemplateService templateService) =>
{
    try
    {
        var result = await templateService.RenderTemplateAsync(request.TemplateName, request.Parameters);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        staticServiceLogger.LogWarning("Invalid render request: {Message}", ex.Message);
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error rendering prompt template: {Message}", ex.Message);
        return Results.Problem($"Failed to render template: {ex.Message}");
    }
})
.WithName("PromptsRender")
.WithOpenApi()
.WithSummary("Render Prompt Template")
.WithDescription("Render a prompt template with provided parameters");

app.MapPost("/mcp/prompts/validate", (RenderPromptRequest request, PromptTemplateService templateService) =>
{
    try
    {
        var template = templateService.GetTemplate(request.TemplateName);
        if (template == null)
        {
            return Results.NotFound($"Template '{request.TemplateName}' not found");
        }

        var validation = templateService.ValidateParameters(template, request.Parameters);
        return Results.Ok(validation);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error validating prompt parameters: {Message}", ex.Message);
        return Results.Problem($"Failed to validate parameters: {ex.Message}");
    }
})
.WithName("PromptsValidate")
.WithOpenApi()
.WithSummary("Validate Prompt Parameters")
.WithDescription("Validate parameters against a prompt template's requirements");

// Prompt Analysis Integration Endpoints
app.MapPost("/mcp/prompts/analyze", async (DocumentAnalysisRequest request, PromptAnalysisService analysisService, string templateName) =>
{
    try
    {
        var result = await analysisService.AnalyzeDocumentAsync(templateName, request);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        staticServiceLogger.LogWarning("Invalid analysis request: {Message}", ex.Message);
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error analyzing document: {Message}", ex.Message);
        return Results.Problem($"Failed to analyze document: {ex.Message}");
    }
})
.WithName("PromptsAnalyze")
.WithOpenApi()
.WithSummary("Analyze Document with Prompt Template")
.WithDescription("Analyze a document using AI with a specific prompt template");

app.MapGet("/mcp/prompts/suggestions", async (PromptAnalysisService analysisService, string documentType, string? companyName) =>
{
    try
    {
        var suggestions = await analysisService.GetAnalysisSuggestionsAsync(documentType, companyName ?? "");
        return Results.Ok(new { suggestions });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting analysis suggestions: {Message}", ex.Message);
        return Results.Problem($"Failed to get suggestions: {ex.Message}");
    }
})
.WithName("PromptsAnalysisSuggestions")
.WithOpenApi()
.WithSummary("Get Analysis Suggestions")
.WithDescription("Get recommended prompt templates for a specific document type");

app.MapPost("/mcp/prompts/batch-analyze", async (List<DocumentAnalysisRequest> requests, PromptAnalysisService analysisService, string templateName) =>
{
    try
    {
        var results = await analysisService.BatchAnalyzeAsync(templateName, requests);
        return Results.Ok(new { results });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error in batch analysis: {Message}", ex.Message);
        return Results.Problem($"Failed to perform batch analysis: {ex.Message}");
    }
})
.WithName("PromptsBatchAnalyze")
.WithOpenApi()
.WithSummary("Batch Analyze Documents")
.WithDescription("Analyze multiple documents using the same prompt template");

app.Run();
