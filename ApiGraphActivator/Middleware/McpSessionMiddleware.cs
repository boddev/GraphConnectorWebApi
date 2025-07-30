using ApiGraphActivator.Services.Mcp;

namespace ApiGraphActivator.Middleware;

/// <summary>
/// Middleware for validating MCP sessions and updating activity
/// </summary>
public class McpSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpSessionMiddleware> _logger;

    public McpSessionMiddleware(RequestDelegate next, ILogger<McpSessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        // Check if this is an MCP endpoint that requires session validation
        if (ShouldValidateSession(context.Request.Path))
        {
            var sessionId = ExtractSessionId(context);
            if (!string.IsNullOrEmpty(sessionId))
            {
                try
                {
                    var sessionManager = serviceProvider.GetService<ISessionManager>();
                    var connectionManager = serviceProvider.GetService<IConnectionManager>();

                    if (sessionManager != null)
                    {
                        var validation = await sessionManager.ValidateSessionAsync(sessionId);
                        if (validation.IsValid)
                        {
                            // Update session activity
                            await sessionManager.UpdateSessionActivityAsync(sessionId);
                            
                            // Store session info in context for use by endpoints
                            context.Items["McpSession"] = validation.Session;
                            context.Items["McpSessionValid"] = true;

                            // Update connection activity if connection ID is available
                            var connectionId = ExtractConnectionId(context);
                            if (!string.IsNullOrEmpty(connectionId) && connectionManager != null)
                            {
                                await connectionManager.UpdateConnectionActivityAsync(connectionId);
                            }
                        }
                        else
                        {
                            context.Items["McpSessionValid"] = false;
                            context.Items["McpSessionError"] = validation.ErrorMessage;
                            
                            if (validation.RequiresAuthentication)
                            {
                                context.Items["McpRequiresAuth"] = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating session {SessionId}", sessionId);
                    context.Items["McpSessionValid"] = false;
                    context.Items["McpSessionError"] = "Session validation failed";
                }
            }
        }

        await _next(context);
    }

    private bool ShouldValidateSession(string path)
    {
        // Validate sessions for MCP endpoints and protected operations
        var protectedPaths = new[]
        {
            "/mcp/",
            "/loadcontent",
            "/provisionconnection",
            "/crawl-",
            "/recrawl-all"
        };

        return protectedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractSessionId(HttpContext context)
    {
        // Try to get session ID from various sources
        
        // 1. From X-MCP-Session-Id header
        var sessionId = context.Request.Headers["X-MCP-Session-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(sessionId))
            return sessionId;

        // 2. From query parameter
        sessionId = context.Request.Query["sessionId"].FirstOrDefault();
        if (!string.IsNullOrEmpty(sessionId))
            return sessionId;

        // 3. From route parameter (for MCP endpoints)
        if (context.Request.RouteValues.TryGetValue("sessionId", out var routeSessionId))
            return routeSessionId?.ToString();

        return null;
    }

    private string? ExtractConnectionId(HttpContext context)
    {
        // Try to get connection ID from X-MCP-Connection-Id header
        return context.Request.Headers["X-MCP-Connection-Id"].FirstOrDefault();
    }
}

/// <summary>
/// Extension methods for adding MCP session middleware
/// </summary>
public static class McpSessionMiddlewareExtensions
{
    public static IApplicationBuilder UseMcpSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<McpSessionMiddleware>();
    }
}