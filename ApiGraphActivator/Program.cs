using ApiGraphActivator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

// Register the custom logging service
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<BackgroundTaskQueue>(sp => new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//var logger = app.Services.GetRequiredService<LoggingService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started and Application Insights is configured.");
EdgarService.InitializeLogger(logger);
ConnectionService.InitializeLogger(logger);

app.MapGet("/", () => "Hello World!")
    .WithName("GetHelloWorld")
    .WithOpenApi();

app.MapPost("/grantPermissions", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var tenantId = await reader.ReadToEndAsync();

    // Log the tenant ID
    logger.LogInformation("Received tenant ID: {TenantId}", tenantId);

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
    logger.LogInformation("Provisioning connection for tenant ID: {TenantId}", tenantId);

    // Call the ProvisionConnection method with the tenant ID
    try{
        await ConnectionService.ProvisionConnection();
        await context.Response.WriteAsync("Connection provisioned successfully.");
    } catch (Exception ex) {
        // Log the exception message
        logger.LogError("Error provisioning connection: {Message}", ex.Message);
        await context.Response.WriteAsync($"Error provisioning connection: {ex.Message}");
    }
})
.WithName("provisionconnection")
.WithOpenApi();

app.MapPost("/loadcontent", async (HttpContext context, BackgroundTaskQueue taskQueue) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var tenantId = await reader.ReadToEndAsync();
    // Log the tenant ID
    logger.LogInformation("Loading content for tenant ID: {TenantId}", tenantId);

    // Queue the long-running task
    await taskQueue.QueueBackgroundWorkItemAsync(async token =>
    {
        await ContentService.LoadContent();
    });

    // Return a response immediately
    context.Response.StatusCode = StatusCodes.Status202Accepted;
})
.WithName("loadcontent")
.WithOpenApi();
app.Run();
