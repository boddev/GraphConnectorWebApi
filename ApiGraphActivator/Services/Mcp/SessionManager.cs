using System.Collections.Concurrent;
using System.Text.Json;
using ApiGraphActivator.Models.Mcp;
using Microsoft.Extensions.Options;

namespace ApiGraphActivator.Services.Mcp;

/// <summary>
/// In-memory implementation of session management for MCP clients
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, McpSession> _sessions = new();
    private readonly ILogger<SessionManager> _logger;
    private readonly SessionCleanupConfiguration _config;
    private readonly ISessionAuthenticationProvider _authProvider;

    public SessionManager(
        ILogger<SessionManager> logger,
        IOptions<SessionCleanupConfiguration> config,
        ISessionAuthenticationProvider authProvider)
    {
        _logger = logger;
        _config = config.Value;
        _authProvider = authProvider;
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request)
    {
        try
        {
            _logger.LogInformation("Creating session for client {ClientId}", request.ClientId);

            // Check if client has too many active sessions
            var existingSessions = await GetActiveSessionsAsync(request.ClientId);
            if (existingSessions.Count >= _config.MaxSessionsPerClient)
            {
                _logger.LogWarning("Client {ClientId} has reached maximum session limit", request.ClientId);
                return new CreateSessionResponse
                {
                    Success = false,
                    ErrorMessage = $"Maximum number of sessions ({_config.MaxSessionsPerClient}) reached for client"
                };
            }

            var session = new McpSession
            {
                ClientId = request.ClientId,
                UserId = request.UserId,
                TenantId = request.TenantId,
                ExpiresAt = DateTime.UtcNow.Add(
                    request.TimeoutMinutes.HasValue 
                        ? TimeSpan.FromMinutes(request.TimeoutMinutes.Value)
                        : _config.DefaultSessionTimeout),
                Capabilities = DetermineGrantedCapabilities(request.RequestedCapabilities)
            };

            _sessions.TryAdd(session.SessionId, session);

            _logger.LogInformation("Session {SessionId} created successfully for client {ClientId}", 
                session.SessionId, request.ClientId);

            return new CreateSessionResponse
            {
                SessionId = session.SessionId,
                ExpiresAt = session.ExpiresAt,
                GrantedCapabilities = session.Capabilities,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for client {ClientId}", request.ClientId);
            return new CreateSessionResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while creating the session"
            };
        }
    }

    public async Task<SessionValidationResult> ValidateSessionAsync(string sessionId)
    {
        await Task.CompletedTask; // Make async

        if (string.IsNullOrEmpty(sessionId))
        {
            return new SessionValidationResult
            {
                IsValid = false,
                ErrorMessage = "Session ID is required"
            };
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return new SessionValidationResult
            {
                IsValid = false,
                ErrorMessage = "Session not found"
            };
        }

        if (session.State != McpSessionState.Active)
        {
            _logger.LogWarning("Session {SessionId} is not active (state: {State})", sessionId, session.State);
            return new SessionValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Session is {session.State.ToString().ToLower()}"
            };
        }

        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _logger.LogWarning("Session {SessionId} has expired", sessionId);
            session.State = McpSessionState.Expired;
            return new SessionValidationResult
            {
                IsValid = false,
                ErrorMessage = "Session has expired"
            };
        }

        // Check if authentication is required and valid
        bool requiresAuth = session.AuthenticationInfo?.IsAuthenticated != true;
        if (requiresAuth && session.TenantId != null)
        {
            return new SessionValidationResult
            {
                IsValid = false,
                Session = session,
                RequiresAuthentication = true,
                ErrorMessage = "Authentication required"
            };
        }

        _logger.LogTrace("Session {SessionId} validation successful", sessionId);
        return new SessionValidationResult
        {
            IsValid = true,
            Session = session
        };
    }

    public async Task UpdateSessionActivityAsync(string sessionId)
    {
        await Task.CompletedTask; // Make async

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastAccessedAt = DateTime.UtcNow;
            _logger.LogTrace("Updated activity for session {SessionId}", sessionId);
        }
    }

    public async Task<bool> TerminateSessionAsync(string sessionId)
    {
        await Task.CompletedTask; // Make async

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.State = McpSessionState.Terminated;
            _sessions.TryRemove(sessionId, out _);
            _logger.LogInformation("Session {SessionId} terminated", sessionId);
            return true;
        }

        return false;
    }

    public async Task<T?> GetSessionDataAsync<T>(string sessionId, string key)
    {
        await Task.CompletedTask; // Make async

        if (_sessions.TryGetValue(sessionId, out var session) && 
            session.SessionData.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T directValue)
                    return directValue;

                if (value is JsonElement jsonElement)
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());

                var json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing session data for key {Key} in session {SessionId}", key, sessionId);
            }
        }

        return default;
    }

    public async Task SetSessionDataAsync(string sessionId, string key, object value)
    {
        await Task.CompletedTask; // Make async

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.SessionData[key] = value;
            session.LastAccessedAt = DateTime.UtcNow;
            _logger.LogTrace("Set session data {Key} for session {SessionId}", key, sessionId);
        }
    }

    public async Task RemoveSessionDataAsync(string sessionId, string key)
    {
        await Task.CompletedTask; // Make async

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.SessionData.Remove(key);
            session.LastAccessedAt = DateTime.UtcNow;
            _logger.LogTrace("Removed session data {Key} for session {SessionId}", key, sessionId);
        }
    }

    public async Task<List<McpSession>> GetActiveSessionsAsync(string clientId)
    {
        await Task.CompletedTask; // Make async

        return _sessions.Values
            .Where(s => s.ClientId == clientId && s.State == McpSessionState.Active)
            .ToList();
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        await Task.CompletedTask; // Make async

        var now = DateTime.UtcNow;
        var expiredSessions = _sessions.Values
            .Where(s => now > s.ExpiresAt || 
                       (now - s.LastAccessedAt) > _config.InactivityTimeout)
            .ToList();

        foreach (var session in expiredSessions)
        {
            session.State = McpSessionState.Expired;
            _sessions.TryRemove(session.SessionId, out _);
            _logger.LogDebug("Cleaned up expired session {SessionId}", session.SessionId);
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        }
    }

    public async Task<SessionStatistics> GetSessionStatisticsAsync()
    {
        await Task.CompletedTask; // Make async

        var activeSessions = _sessions.Values.Where(s => s.State == McpSessionState.Active).ToList();
        var sessionsByClient = activeSessions
            .GroupBy(s => s.ClientId)
            .ToDictionary(g => g.Key, g => g.Count());

        var avgDuration = activeSessions.Any()
            ? TimeSpan.FromTicks((long)activeSessions.Average(s => (DateTime.UtcNow - s.CreatedAt).Ticks))
            : TimeSpan.Zero;

        return new SessionStatistics
        {
            TotalActiveSessions = activeSessions.Count,
            SessionsByClient = sessionsByClient,
            AverageSessionDuration = avgDuration
        };
    }

    private List<string> DetermineGrantedCapabilities(List<string> requestedCapabilities)
    {
        // Define available capabilities for this MCP server
        var availableCapabilities = new[]
        {
            "edgar-search",
            "document-processing", 
            "company-data",
            "filing-extraction",
            "content-indexing"
        };

        return requestedCapabilities
            .Where(cap => availableCapabilities.Contains(cap))
            .ToList();
    }
}