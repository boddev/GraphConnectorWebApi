using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ApiGraphActivator;
using System.Text.Json;

// Check for MCP stdio mode early, before building web application
if (args.Contains("--mcp-stdio"))
{
    return await RunStdioModeAsync(args);
}

// Continue with normal web application setup
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

// Register MCP Server Service
builder.Services.AddHttpClient();
builder.Services.AddScoped<MCPServerService>();
builder.Services.AddScoped<CopilotChatService>();
builder.Services.AddScoped<DocumentSearchService>();
builder.Services.AddScoped<ExternalConnectionManagerService>();

// Register MCP Transport Service for stdio mode
builder.Services.AddScoped<MCPTransportService>();

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

// Serve static files from wwwroot (React app)
app.UseStaticFiles();

// Enable default files (serves index.html by default)
app.UseDefaultFiles();

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

// Configure SPA fallback routing - serve React app for unmatched routes
app.MapFallbackToFile("index.html");

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
            
            // Require explicit connectionId (no implicit default)
            if (string.IsNullOrWhiteSpace(crawlRequest.ConnectionId))
            {
                staticServiceLogger.LogWarning("No connectionId provided in crawl request");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("connectionId is required");
                return;
            }

            var targetConnectionId = crawlRequest.ConnectionId;
            
            staticServiceLogger.LogInformation("Target connection ID: {ConnectionId}", targetConnectionId ?? "default");
            
            // Save companies to config file for persistence with connection ID
            await ConfigurationService.SaveCrawledCompaniesAsync(crawlRequest.Companies, targetConnectionId);
            
            staticServiceLogger.LogInformation("Queueing background task for crawl");
            
            // Queue the long-running task with selected companies
            await taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                staticServiceLogger.LogInformation("Background task started for {CompanyCount} companies in connection {ConnectionId}", 
                    crawlRequest.Companies.Count, targetConnectionId ?? "default");
                await ContentService.LoadContentForCompanies(crawlRequest.Companies, targetConnectionId);
                staticServiceLogger.LogInformation("Background task completed for {CompanyCount} companies in connection {ConnectionId}", 
                    crawlRequest.Companies.Count, targetConnectionId ?? "default");
            });
            
            staticServiceLogger.LogInformation("Background task queued successfully");
        }
        else
        {
            // Return error instead of performing full crawl
            staticServiceLogger.LogWarning("No companies specified in crawl request");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("No companies specified for crawl. Please select companies to crawl or use the /recrawl-all endpoint for previously crawled companies.");
            return;
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
        
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        
        // Parse connection ID if provided
        string? connectionId = null;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var request = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody, options);
                connectionId = request?.GetValueOrDefault("connectionId");
            }
            catch
            {
                // Treat as plain connection ID string if not JSON
                connectionId = requestBody.Trim();
            }
        }
        
        staticServiceLogger.LogInformation("Recrawling for connection: {ConnectionId}", connectionId ?? "default");

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("connectionId is required");
            return;
        }
        
        // Load previously crawled companies for the specific connection
        var config = await ConfigurationService.LoadCrawledCompaniesAsync(connectionId);
        
        if (config?.Companies?.Any() != true)
        {
            staticServiceLogger.LogWarning("No previously crawled companies found for connection {ConnectionId}", connectionId ?? "default");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"No previously crawled companies found for connection {connectionId ?? "default"}. Please crawl companies first.");
            return;
        }

        staticServiceLogger.LogInformation("Starting recrawl for {CompanyCount} previously crawled companies in connection {ConnectionId}", 
            config.Companies.Count, connectionId ?? "default");
        
        // Queue the background task with previously crawled companies
        await taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            staticServiceLogger.LogInformation("Background recrawl task started for {CompanyCount} companies in connection {ConnectionId}", 
                config.Companies.Count, connectionId ?? "default");
            await ContentService.LoadContentForCompanies(config.Companies, connectionId);
            staticServiceLogger.LogInformation("Background recrawl task completed for {CompanyCount} companies in connection {ConnectionId}", 
                config.Companies.Count, connectionId ?? "default");
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

app.MapPost("/full-crawl", async (HttpContext context, BackgroundTaskQueue taskQueue) =>
{
    try
    {
        staticServiceLogger.LogInformation("Received explicit full-crawl request");
        
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        
        // Parse connection ID if provided
        string? connectionId = null;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var request = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody, options);
                connectionId = request?.GetValueOrDefault("connectionId");
            }
            catch
            {
                // Treat as plain connection ID string if not JSON
                connectionId = requestBody.Trim();
            }
        }
        
        staticServiceLogger.LogInformation("Starting explicit full crawl for connection: {ConnectionId}", connectionId ?? "default");

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("connectionId is required");
            return;
        }
        
        // Queue the full crawl task
        await taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            staticServiceLogger.LogInformation("Background full crawl task started for connection: {ConnectionId}", connectionId ?? "default");
            try
            {
                await ContentService.LoadContent(connectionId);
                staticServiceLogger.LogInformation("Background full crawl task completed for connection: {ConnectionId}", connectionId ?? "default");
            }
            catch (Exception ex)
            {
                staticServiceLogger.LogError(ex, "Error in background full crawl task for connection: {ConnectionId}", connectionId ?? "default");
            }
        });
        
        staticServiceLogger.LogInformation("Background full crawl task queued successfully");
        
        // Return a response immediately
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        await context.Response.WriteAsync($"Full crawl started successfully for connection: {connectionId ?? "default"}");
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error starting full crawl: {Message}", ex.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error starting full crawl: {ex.Message}");
    }
})
.WithName("FullCrawl")
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
        // Get connectionId from query parameter
        string? connectionId = context.Request.Query["connectionId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("connectionId query parameter is required");
            return;
        }
        
        staticServiceLogger.LogInformation("Fetching previously crawled companies for connection: {ConnectionId}", connectionId ?? "default");
        var config = await ConfigurationService.LoadCrawledCompaniesAsync(connectionId);
        
        if (config == null)
        {
            var emptyResult = new { 
                lastCrawlDate = (DateTime?)null,
                companies = new List<Company>(),
                totalCompanies = 0,
                connectionId = connectionId ?? "default"
            };
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(emptyResult));
            return;
        }

        var result = new {
            lastCrawlDate = config.LastCrawlDate,
            companies = config.Companies,
            totalCompanies = config.TotalCompanies,
            connectionId = connectionId ?? "default"
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

// External Connection Management endpoints
app.MapGet("/external-connections", async (ExternalConnectionManagerService connectionManager) =>
{
    try
    {
        var connections = await connectionManager.GetConnectionsAsync();
        return Results.Ok(connections);
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error getting external connections: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("GetExternalConnections")
.WithOpenApi();

app.MapPost("/external-connections", async (CreateExternalConnectionRequest request, ExternalConnectionManagerService connectionManager) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest("Connection ID and Name are required");
        }

        var result = await connectionManager.CreateConnectionAsync(request);
        if (result.Success)
        {
            staticServiceLogger.LogInformation("Successfully created external connection: {ConnectionId}", request.Id);
            return Results.Ok(result.Result);
        }
        else
        {
            staticServiceLogger.LogError("Failed to create external connection: {Error}", result.ErrorMessage);
            return Results.BadRequest(result.ErrorMessage);
        }
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error creating external connection: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("CreateExternalConnection")
.WithOpenApi();

app.MapDelete("/external-connections/{connectionId}", async (string connectionId, ExternalConnectionManagerService connectionManager) =>
{
    try
    {
        var result = await connectionManager.DeleteConnectionAsync(connectionId);
        if (result.Success)
        {
            staticServiceLogger.LogInformation("Successfully deleted external connection: {ConnectionId}", connectionId);
            return Results.Ok();
        }
        else
        {
            staticServiceLogger.LogError("Failed to delete external connection: {Error}", result.ErrorMessage);
            return Results.BadRequest(result.ErrorMessage);
        }
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError("Error deleting external connection: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
})
.WithName("DeleteExternalConnection")
.WithOpenApi();

// Enhanced loadcontent endpoint with connection selection
app.MapPost("/loadcontent-to-connection", async (HttpContext context, BackgroundTaskQueue taskQueue) =>
{
    try
    {
        string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        staticServiceLogger.LogInformation("Received loadcontent-to-connection request: {RequestBody}", requestBody);

        // Use the configured JSON options for deserialization
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var request = JsonSerializer.Deserialize<LoadContentToConnectionRequest>(requestBody, jsonOptions);
        
        staticServiceLogger.LogInformation("Deserialized request - Companies: {CompanyCount}, ConnectionId: {ConnectionId}", 
            request?.Companies?.Count ?? -1, request?.ConnectionId ?? "null");
        
        if (request?.Companies == null || !request.Companies.Any())
        {
            staticServiceLogger.LogWarning("No companies provided in loadcontent-to-connection request");
            return Results.BadRequest("At least one company must be provided");
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            staticServiceLogger.LogWarning("No connection ID provided in loadcontent-to-connection request");
            return Results.BadRequest("Connection ID is required");
        }

        staticServiceLogger.LogInformation("Queuing content loading for {CompanyCount} companies to connection: {ConnectionId}", 
            request.Companies.Count, request.ConnectionId);
        
        // Queue the work item
        await taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            staticServiceLogger.LogInformation("Starting background content loading for {CompanyCount} companies to connection: {ConnectionId}", 
                request.Companies.Count, request.ConnectionId);
            
            try
            {
                await ContentService.LoadContentForCompanies(request.Companies, request.ConnectionId);
                staticServiceLogger.LogInformation("Successfully completed content loading for {CompanyCount} companies to connection: {ConnectionId}", 
                    request.Companies.Count, request.ConnectionId);
            }
            catch (Exception ex)
            {
                staticServiceLogger.LogError(ex, "Error in background content loading task for connection: {ConnectionId}", request.ConnectionId);
            }
        });
        
        return Results.Ok(new { 
            message = "Content loading started in background", 
            companyCount = request.Companies.Count,
            connectionId = request.ConnectionId 
        });
    }
    catch (Exception ex)
    {
        staticServiceLogger.LogError(ex, "Error in loadcontent-to-connection endpoint");
        return Results.StatusCode(500);
    }
})
.WithName("LoadContentToConnection")
.WithOpenApi();

// Add MCP Server endpoint
app.MapPost("/mcp", async (MCPRequest request, MCPServerService mcpService) =>
{
    return await mcpService.HandleRequest(request);
})
.WithName("MCPServer")
.WithOpenApi();

// Add MCP Server endpoint
app.MapPost("/discover", async (MCPRequest request, MCPServerService mcpService) =>
{
    return await mcpService.HandleRequest(request);
})
.WithName("MCPDiscover")
.WithOpenApi();

app.Run();
return 0;

/// <summary>
/// Runs the MCP server in stdio transport mode
/// </summary>
static async Task<int> RunStdioModeAsync(string[] args)
{
    // Create a minimal host builder for stdio mode
    var hostBuilder = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // Add logging
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });

            // Register required services for MCP
            services.AddSingleton<LoggingService>();
            services.AddSingleton<StorageConfigurationService>();
            services.AddHttpClient();
            services.AddScoped<MCPServerService>();
            services.AddScoped<CopilotChatService>();
            services.AddScoped<DocumentSearchService>();
            services.AddScoped<ExternalConnectionManagerService>();
            services.AddScoped<MCPTransportService>();
        });

    using var host = hostBuilder.Build();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Starting MCP server in stdio transport mode");
    
    try
    {
        var transportService = host.Services.GetRequiredService<MCPTransportService>();
        using var cts = new CancellationTokenSource();
        
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            logger.LogInformation("Shutting down MCP stdio transport...");
        };
        
        await transportService.RunStdioTransportAsync(cts.Token);
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Fatal error in MCP stdio transport");
        return 1;
    }
}
