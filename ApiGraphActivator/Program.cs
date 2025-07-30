using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ApiGraphActivator;
using System.Text.Json;
using ApiGraphActivator.McpTools;
using ApiGraphActivator.McpResources;

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

// Register OpenAI service for AI analysis
builder.Services.AddScoped<OpenAIService>();

// Register existing MCP search tools
builder.Services.AddScoped<CompanySearchTool>();
builder.Services.AddScoped<FormFilterTool>();
builder.Services.AddScoped<ContentSearchTool>();


// Register new AI analysis tools
builder.Services.AddScoped<DocumentSummarizationTool>();
builder.Services.AddScoped<DocumentQuestionAnswerTool>();
builder.Services.AddScoped<DocumentComparisonTool>();
builder.Services.AddScoped<FinancialAnalysisTool>();
// Register MCP resource services
builder.Services.AddScoped<ApiGraphActivator.McpResources.ResourceService>();

// Register M365 Copilot Client services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IM365CopilotClient>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<M365CopilotClient>>();
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    return new M365CopilotClient(logger, httpClient);
}

// Register conversation management services
builder.Services.AddScoped<ConversationService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<ConversationService>>();
    var storageConfigService = serviceProvider.GetRequiredService<StorageConfigurationService>();
    var storageService = storageConfigService.GetStorageServiceAsync().GetAwaiter().GetResult();
    var documentSearchService = serviceProvider.GetRequiredService<DocumentSearchService>();
    return new ConversationService(logger, storageService, documentSearchService);
});


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

// Initialize M365 Copilot Client
try
{
    using var scope = app.Services.CreateScope();
    var copilotClient = scope.ServiceProvider.GetRequiredService<IM365CopilotClient>();
    var initialized = await copilotClient.InitializeAsync();
    if (initialized)
    {
        staticServiceLogger.LogInformation("M365 Copilot Client initialized successfully");
    }
    else
    {
        staticServiceLogger.LogWarning("M365 Copilot Client initialization failed");
    }
}
catch (Exception ex)
{
    staticServiceLogger.LogWarning("Failed to initialize M365 Copilot Client: {Message}", ex.Message);
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

// Conversation Management Endpoints
app.MapPost("/conversations/sessions", async (ConversationService conversationService, CreateSessionRequest? request) =>
{
    try
    {
        var ttl = request?.TtlHours > 0 ? TimeSpan.FromHours(request.TtlHours.Value) : (TimeSpan?)null;
        var session = await conversationService.CreateSessionAsync(request?.UserId, ttl);
        return Results.Created($"/conversations/sessions/{session.Id}", session);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error creating conversation session: {Message}", ex.Message);
        return Results.Problem($"Failed to create session: {ex.Message}");
    }
})
.WithName("CreateConversationSession")
.WithOpenApi()
.WithSummary("Create a new conversation session")
.WithDescription("Creates a new conversation session for multi-turn interactions");

app.MapGet("/conversations/sessions/{sessionId}", async (string sessionId, ConversationService conversationService) =>
{
    try
    {
        var session = await conversationService.GetSessionAsync(sessionId);
        return session != null ? Results.Ok(session) : Results.NotFound($"Session {sessionId} not found");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting conversation session {SessionId}: {Message}", sessionId, ex.Message);
        return Results.Problem($"Failed to get session: {ex.Message}");
    }
})
.WithName("GetConversationSession")
.WithOpenApi()
.WithSummary("Get a conversation session")
.WithDescription("Retrieves an existing conversation session by ID");

app.MapPost("/conversations/sessions/{sessionId}/conversations", async (string sessionId, ConversationService conversationService, CreateConversationRequest? request) =>
{
    try
    {
        var conversation = await conversationService.CreateConversationAsync(sessionId, request?.Title);
        return Results.Created($"/conversations/{conversation.Id}", conversation);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error creating conversation in session {SessionId}: {Message}", sessionId, ex.Message);
        return Results.Problem($"Failed to create conversation: {ex.Message}");
    }
})
.WithName("CreateConversation")
.WithOpenApi()
.WithSummary("Create a conversation in a session")
.WithDescription("Creates a new conversation within an existing session");

app.MapGet("/conversations/{conversationId}", async (string conversationId, ConversationService conversationService, int skip = 0, int take = 100) =>
{
    try
    {
        var result = await conversationService.GetConversationWithMessagesAsync(conversationId, skip, take);
        return result != null ? Results.Ok(result) : Results.NotFound($"Conversation {conversationId} not found");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting conversation {ConversationId}: {Message}", conversationId, ex.Message);
        return Results.Problem($"Failed to get conversation: {ex.Message}");
    }
})
.WithName("GetConversationWithMessages")
.WithOpenApi()
.WithSummary("Get a conversation with messages")
.WithDescription("Retrieves a conversation with its messages, supporting pagination");

app.MapPost("/conversations/{conversationId}/messages", async (string conversationId, ConversationService conversationService, AddMessageRequest request) =>
{
    try
    {
        var message = await conversationService.AddMessageAsync(
            conversationId,
            request.Role,
            request.Content,
            request.Citations,
            request.Metadata,
            request.ToolCallId,
            request.ToolName);
        
        return Results.Created($"/conversations/{conversationId}/messages/{message.Id}", message);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error adding message to conversation {ConversationId}: {Message}", conversationId, ex.Message);
        return Results.Problem($"Failed to add message: {ex.Message}");
    }
})
.WithName("AddConversationMessage")
.WithOpenApi()
.WithSummary("Add a message to a conversation")
.WithDescription("Adds a new message to an existing conversation");

app.MapGet("/conversations/sessions/{sessionId}/conversations", async (string sessionId, ConversationService conversationService) =>
{
    try
    {
        var conversations = await conversationService.GetSessionConversationsAsync(sessionId);
        return Results.Ok(conversations);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting conversations for session {SessionId}: {Message}", sessionId, ex.Message);
        return Results.Problem($"Failed to get conversations: {ex.Message}");
    }
})
.WithName("GetSessionConversations")
.WithOpenApi()
.WithSummary("Get conversations for a session")
.WithDescription("Retrieves all conversations within a session");

app.MapPut("/conversations/{conversationId}/context", async (string conversationId, ConversationService conversationService, Dictionary<string, object> contextUpdates) =>
{
    try
    {
        await conversationService.UpdateConversationContextAsync(conversationId, contextUpdates);
        return Results.Ok(new { message = "Context updated successfully" });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error updating context for conversation {ConversationId}: {Message}", conversationId, ex.Message);
        return Results.Problem($"Failed to update context: {ex.Message}");
    }
})
.WithName("UpdateConversationContext")
.WithOpenApi()
.WithSummary("Update conversation context")
.WithDescription("Updates the context data for a conversation");

app.MapPost("/conversations/cleanup", async (ConversationService conversationService) =>
{
    try
    {
        await conversationService.CleanupExpiredSessionsAsync();
        return Results.Ok(new { message = "Cleanup completed successfully" });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error during conversation cleanup: {Message}", ex.Message);
        return Results.Problem($"Cleanup failed: {ex.Message}");
    }
})
.WithName("CleanupExpiredSessions")
.WithOpenApi()
.WithSummary("Cleanup expired sessions")
.WithDescription("Removes expired conversation sessions and their data");

app.MapGet("/conversations/metrics", async (ConversationService conversationService) =>
{
    try
    {
        var metrics = await conversationService.GetConversationMetricsAsync();
        return Results.Ok(metrics);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting conversation metrics: {Message}", ex.Message);
        return Results.Problem($"Failed to get metrics: {ex.Message}");
    }
})
.WithName("GetConversationMetrics")
.WithOpenApi()
.WithSummary("Get conversation system metrics")
.WithDescription("Retrieves metrics about the conversation system usage");

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

// MCP AI Analysis Tool Endpoints
app.MapPost("/mcp/tools/document-summarization", async (DocumentSummarizationParameters parameters, DocumentSummarizationTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing document summarization tool: {Message}", ex.Message);
        return Results.Problem($"Document summarization failed: {ex.Message}");
    }
})
.WithName("McpDocumentSummarization")
.WithOpenApi()
.WithSummary("MCP Tool: AI Document Summarization")
.WithDescription("Generate comprehensive summaries of SEC filing documents using AI analysis with different summary types");

app.MapPost("/mcp/tools/document-qa", async (DocumentQuestionAnswerParameters parameters, DocumentQuestionAnswerTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing document Q&A tool: {Message}", ex.Message);
        return Results.Problem($"Document Q&A failed: {ex.Message}");
    }
})
.WithName("McpDocumentQuestionAnswer")
.WithOpenApi()
.WithSummary("MCP Tool: AI Document Question Answering")
.WithDescription("Answer specific questions about SEC filing documents using AI analysis with citations and supporting evidence");

app.MapPost("/mcp/tools/document-comparison", async (DocumentComparisonParameters parameters, DocumentComparisonTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing document comparison tool: {Message}", ex.Message);
        return Results.Problem($"Document comparison failed: {ex.Message}");
    }
})
.WithName("McpDocumentComparison")
.WithOpenApi()
.WithSummary("MCP Tool: AI Document Comparison")
.WithDescription("Perform comparative analysis across multiple SEC filing documents to identify differences, similarities, and trends");

app.MapPost("/mcp/tools/financial-analysis", async (FinancialAnalysisParameters parameters, FinancialAnalysisTool tool) =>
{
    try
    {
        var result = await tool.ExecuteAsync(parameters);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error executing financial analysis tool: {Message}", ex.Message);
        return Results.Problem($"Financial analysis failed: {ex.Message}");
    }
})
.WithName("McpFinancialAnalysis")
.WithOpenApi()
.WithSummary("MCP Tool: AI Financial Analysis")
.WithDescription("Perform comprehensive financial analysis of SEC filing documents with key metrics, ratios, and business insights");

// MCP Tools Discovery Endpoint
app.MapGet("/mcp/tools", (CompanySearchTool companyTool, FormFilterTool formTool, ContentSearchTool contentTool,
    DocumentSummarizationTool summarizationTool, DocumentQuestionAnswerTool qaTool, 
    DocumentComparisonTool comparisonTool, FinancialAnalysisTool financialTool) =>
{
    var tools = new[]
    {
        // Document Search Tools
        new
        {
            name = companyTool.Name,
            description = companyTool.Description,
            inputSchema = companyTool.InputSchema,
            endpoint = "/mcp/tools/company-search",
            category = "Document Search"
        },
        new
        {
            name = formTool.Name,
            description = formTool.Description,
            inputSchema = formTool.InputSchema,
            endpoint = "/mcp/tools/form-filter",
            category = "Document Search"
        },
        new
        {
            name = contentTool.Name,
            description = contentTool.Description,
            inputSchema = contentTool.InputSchema,
            endpoint = "/mcp/tools/content-search",
            category = "Document Search"
        },
        // AI Analysis Tools
        new
        {
            name = summarizationTool.Name,
            description = summarizationTool.Description,
            inputSchema = summarizationTool.InputSchema,
            endpoint = "/mcp/tools/document-summarization",
            category = "AI Analysis"
        },
        new
        {
            name = qaTool.Name,
            description = qaTool.Description,
            inputSchema = qaTool.InputSchema,
            endpoint = "/mcp/tools/document-qa",
            category = "AI Analysis"
        },
        new
        {
            name = comparisonTool.Name,
            description = comparisonTool.Description,
            inputSchema = comparisonTool.InputSchema,
            endpoint = "/mcp/tools/document-comparison",
            category = "AI Analysis"
        },
        new
        {
            name = financialTool.Name,
            description = financialTool.Description,
            inputSchema = financialTool.InputSchema,
            endpoint = "/mcp/tools/financial-analysis",
            category = "AI Analysis"
        }
    };

    return Results.Ok(new { 
        tools, 
        totalCount = tools.Length,
        categories = new[] { "Document Search", "AI Analysis" },
        description = "MCP tools for SEC document search and AI-powered analysis including summarization, Q&A, comparison, and financial analysis"
    });
})
.WithName("McpToolsDiscovery")
.WithOpenApi()
.WithSummary("MCP Tools Discovery")
.WithDescription("List all available MCP document search and AI analysis tools with their schemas and endpoints");


// MCP Resources Endpoints
app.MapGet("/mcp/resources/list", async (
    ApiGraphActivator.McpResources.ResourceService resourceService,
    string? companyName = null,
    string? formType = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    int limit = 100,
    int offset = 0,
    string sortBy = "filingDate",
    string sortOrder = "desc") =>
{
    try
    {
        var parameters = new ApiGraphActivator.McpResources.ResourceListParameters
        {
            CompanyName = companyName,
            FormType = formType,
            StartDate = startDate,
            EndDate = endDate,
            Limit = Math.Min(limit, 1000),
            Offset = Math.Max(offset, 0),
            SortBy = sortBy,
            SortOrder = sortOrder
        };

        var result = await resourceService.ListResourcesAsync(parameters);
        
        if (result.IsError)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(result.Content);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error in resources/list endpoint: {Message}", ex.Message);
        return Results.Problem($"Internal server error: {ex.Message}");
    }
})
.WithName("McpResourcesList")
.WithOpenApi()
.WithSummary("MCP Resources List")
.WithDescription("List available SEC Edgar document resources with optional filtering and pagination");

app.MapGet("/mcp/resources/content/{**resourceUri}", async (
    string resourceUri,
    ApiGraphActivator.McpResources.ResourceService resourceService) =>
{
    try
    {
        // Decode the resource URI
        var decodedUri = Uri.UnescapeDataString(resourceUri);
        
        var result = await resourceService.GetResourceContentAsync(decodedUri);
        
        if (result.IsError)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(result.Content);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error in resources/content endpoint: {Message}", ex.Message);
        return Results.Problem($"Internal server error: {ex.Message}");
    }
})
.WithName("McpResourceContent")
.WithOpenApi()
.WithSummary("MCP Resource Content")
.WithDescription("Get the full content of a specific SEC Edgar document resource by URI");

app.MapGet("/mcp/resources/metadata/{**resourceUri}", async (
    string resourceUri,
    ApiGraphActivator.McpResources.ResourceService resourceService) =>
{
    try
    {
        // Decode the resource URI
        var decodedUri = Uri.UnescapeDataString(resourceUri);
        
        var result = await resourceService.GetResourceMetadataAsync(decodedUri);
        
        if (result.IsError)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(result.Content);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error in resources/metadata endpoint: {Message}", ex.Message);
        return Results.Problem($"Internal server error: {ex.Message}");
    }
})
.WithName("McpResourceMetadata")
.WithOpenApi()
.WithSummary("MCP Resource Metadata")
.WithDescription("Get metadata for a specific SEC Edgar document resource without the full content");

// MCP Resources Discovery Endpoint
app.MapGet("/mcp/resources", async (ApiGraphActivator.McpResources.ResourceService resourceService) =>
{
    try
    {
        // Return a sample of available resources for discovery
        var parameters = new ApiGraphActivator.McpResources.ResourceListParameters
        {
            Limit = 10,
            Offset = 0,
            SortBy = "filingDate",
            SortOrder = "desc"
        };

        var result = await resourceService.ListResourcesAsync(parameters);
        
        if (result.IsError)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        var response = new
        {
            resourceScheme = "sec-edgar://documents/",
            totalAvailable = result.Content.TotalCount,
            sampleResources = result.Content.Resources.Take(5).Select(r => new
            {
                r.Uri,
                r.Name,
                r.Description,
                r.MimeType
            }),
            endpoints = new
            {
                list = "/mcp/resources/list",
                content = "/mcp/resources/content/{resourceUri}",
                metadata = "/mcp/resources/metadata/{resourceUri}"
            }
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error in resources discovery endpoint: {Message}", ex.Message);
        return Results.Problem($"Internal server error: {ex.Message}");
    }
})
.WithName("McpResourcesDiscovery")
.WithOpenApi()
.WithSummary("MCP Resources Discovery")
.WithDescription("Discover available MCP resources and endpoints");

// M365 Copilot Endpoints
app.MapPost("/copilot/conversations", async (CreateConversationRequest request, IM365CopilotClient copilotClient) =>
{
    try
    {
        var conversation = await copilotClient.CreateConversationAsync(request);
        return Results.Ok(conversation);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error creating Copilot conversation: {Message}", ex.Message);
        return Results.Problem($"Failed to create conversation: {ex.Message}");
    }
})
.WithName("CreateCopilotConversation")
.WithOpenApi()
.WithSummary("Create a new M365 Copilot conversation")
.WithDescription("Create a new conversation for M365 Copilot chat integration");

app.MapGet("/copilot/conversations/{conversationId}", async (string conversationId, IM365CopilotClient copilotClient) =>
{
    try
    {
        var conversation = await copilotClient.GetConversationAsync(conversationId);
        if (conversation == null)
        {
            return Results.NotFound($"Conversation {conversationId} not found");
        }
        return Results.Ok(conversation);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting Copilot conversation {ConversationId}: {Message}", conversationId, ex.Message);
        return Results.Problem($"Failed to get conversation: {ex.Message}");
    }
})
.WithName("GetCopilotConversation")
.WithOpenApi()
.WithSummary("Get a M365 Copilot conversation by ID")
.WithDescription("Retrieve an existing conversation with all messages");

app.MapGet("/copilot/conversations", async (string tenantId, int limit, IM365CopilotClient copilotClient) =>
{
    try
    {
        var conversations = await copilotClient.GetConversationsAsync(tenantId, limit);
        return Results.Ok(conversations);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting Copilot conversations for tenant {TenantId}: {Message}", tenantId, ex.Message);
        return Results.Problem($"Failed to get conversations: {ex.Message}");
    }
})
.WithName("GetCopilotConversations")
.WithOpenApi()
.WithSummary("Get M365 Copilot conversations for a tenant")
.WithDescription("Retrieve all conversations for a specific tenant with optional limit");

app.MapPost("/copilot/chat", async (CopilotChatRequest request, IM365CopilotClient copilotClient) =>
{
    try
    {
        if (request.Stream)
        {
            // Handle streaming response
            return Results.Stream(async stream =>
            {
                var writer = new StreamWriter(stream, leaveOpen: true);
                try
                {
                    await foreach (var chunk in copilotClient.SendMessageStreamAsync(request))
                    {
                        var json = JsonSerializer.Serialize(chunk);
                        await writer.WriteLineAsync($"data: {json}");
                        await writer.FlushAsync();
                        
                        if (chunk.IsComplete)
                        {
                            await writer.WriteLineAsync("data: [DONE]");
                            break;
                        }
                    }
                }
                finally
                {
                    await writer.DisposeAsync();
                }
            }, "text/plain");
        }
        else
        {
            // Handle synchronous response
            var response = await copilotClient.SendMessageAsync(request);
            return Results.Ok(response);
        }
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error sending Copilot message: {Message}", ex.Message);
        return Results.Problem($"Failed to send message: {ex.Message}");
    }
})
.WithName("SendCopilotMessage")
.WithOpenApi()
.WithSummary("Send a message to M365 Copilot")
.WithDescription("Send a message to a conversation and get either synchronous or streaming response");

app.MapDelete("/copilot/conversations/{conversationId}", async (string conversationId, IM365CopilotClient copilotClient) =>
{
    try
    {
        var deleted = await copilotClient.DeleteConversationAsync(conversationId);
        if (!deleted)
        {
            return Results.NotFound($"Conversation {conversationId} not found");
        }
        return Results.Ok(new { message = "Conversation deleted successfully" });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error deleting Copilot conversation {ConversationId}: {Message}", conversationId, ex.Message);
        return Results.Problem($"Failed to delete conversation: {ex.Message}");
    }
})
.WithName("DeleteCopilotConversation")
.WithOpenApi()
.WithSummary("Delete a M365 Copilot conversation")
.WithDescription("Delete a conversation and all its messages");

app.MapGet("/copilot/health", async (IM365CopilotClient copilotClient) =>
{
    try
    {
        var isHealthy = await copilotClient.IsHealthyAsync();
        return Results.Ok(new { 
            healthy = isHealthy, 
            timestamp = DateTime.UtcNow,
            message = isHealthy ? "M365 Copilot Client is healthy" : "M365 Copilot Client is not healthy"
        });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error checking Copilot health: {Message}", ex.Message);
        return Results.Problem($"Health check failed: {ex.Message}");
    }
})
.WithName("CopilotHealthCheck")
.WithOpenApi()
.WithSummary("Check M365 Copilot Client health")
.WithDescription("Check if the Copilot client is authenticated and ready to handle requests");


app.Run();

// Make Program class accessible for testing
public partial class Program { }
