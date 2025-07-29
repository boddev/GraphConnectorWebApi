using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ApiGraphActivator;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddSingleton<BackgroundTaskQueue>(sp => new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();

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
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        
        // Try to parse as JSON first (new format), then fall back to old format
        CrawlRequest? crawlRequest = null;
        try
        {
            crawlRequest = System.Text.Json.JsonSerializer.Deserialize<CrawlRequest>(requestBody);
        }
        catch
        {
            // Fall back to old format (just tenant ID)
            staticServiceLogger.LogInformation("Loading content for tenant ID: {TenantId}", requestBody);
        }

        if (crawlRequest?.Companies?.Any() == true)
        {
            staticServiceLogger.LogInformation("Loading content for {CompanyCount} companies", crawlRequest.Companies.Count);
            
            // Save companies to config file for persistence
            await ConfigurationService.SaveCrawledCompaniesAsync(crawlRequest.Companies);
            
            // Queue the long-running task with selected companies
            await taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ContentService.LoadContentForCompanies(crawlRequest.Companies);
            });
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

app.Run();
