using ApiGraphActivator.Models.Mcp;

namespace ApiGraphActivator.Services.Mcp;

/// <summary>
/// Interface for managing MCP client sessions
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Create a new session for a client
    /// </summary>
    Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request);

    /// <summary>
    /// Validate and retrieve a session by ID
    /// </summary>
    Task<SessionValidationResult> ValidateSessionAsync(string sessionId);

    /// <summary>
    /// Update session last accessed time
    /// </summary>
    Task UpdateSessionActivityAsync(string sessionId);

    /// <summary>
    /// Terminate a session
    /// </summary>
    Task<bool> TerminateSessionAsync(string sessionId);

    /// <summary>
    /// Get session data
    /// </summary>
    Task<T?> GetSessionDataAsync<T>(string sessionId, string key);

    /// <summary>
    /// Set session data
    /// </summary>
    Task SetSessionDataAsync(string sessionId, string key, object value);

    /// <summary>
    /// Remove session data
    /// </summary>
    Task RemoveSessionDataAsync(string sessionId, string key);

    /// <summary>
    /// Get all active sessions for a client
    /// </summary>
    Task<List<McpSession>> GetActiveSessionsAsync(string clientId);

    /// <summary>
    /// Clean up expired sessions
    /// </summary>
    Task CleanupExpiredSessionsAsync();

    /// <summary>
    /// Get session statistics
    /// </summary>
    Task<SessionStatistics> GetSessionStatisticsAsync();
}

/// <summary>
/// Interface for managing MCP connections
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// Register a new connection for a session
    /// </summary>
    Task<string> RegisterConnectionAsync(string sessionId, string remoteEndpoint, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Update connection activity
    /// </summary>
    Task UpdateConnectionActivityAsync(string connectionId);

    /// <summary>
    /// Disconnect a connection
    /// </summary>
    Task DisconnectAsync(string connectionId);

    /// <summary>
    /// Get connections for a session
    /// </summary>
    Task<List<McpConnectionInfo>> GetSessionConnectionsAsync(string sessionId);

    /// <summary>
    /// Get connection information
    /// </summary>
    Task<McpConnectionInfo?> GetConnectionAsync(string connectionId);

    /// <summary>
    /// Clean up inactive connections
    /// </summary>
    Task CleanupInactiveConnectionsAsync();
}

/// <summary>
/// Interface for session authentication
/// </summary>
public interface ISessionAuthenticationProvider
{
    /// <summary>
    /// Authenticate a session with Azure AD
    /// </summary>
    Task<McpAuthenticationInfo?> AuthenticateWithAzureAdAsync(string accessToken);

    /// <summary>
    /// Validate an authentication token
    /// </summary>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// Refresh an expired token
    /// </summary>
    Task<McpAuthenticationInfo?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Extract user claims from token
    /// </summary>
    Task<Dictionary<string, string>> ExtractClaimsAsync(string token);
}

/// <summary>
/// Session statistics
/// </summary>
public class SessionStatistics
{
    public int TotalActiveSessions { get; set; }
    public int TotalConnections { get; set; }
    public Dictionary<string, int> SessionsByClient { get; set; } = new();
    public DateTime LastCleanupTime { get; set; }
    public int ExpiredSessionsCleanedUp { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
}