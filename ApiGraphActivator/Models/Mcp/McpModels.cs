using System.ComponentModel.DataAnnotations;

namespace ApiGraphActivator.Models.Mcp;

/// <summary>
/// Represents a client session in the MCP server
/// </summary>
public class McpSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public McpSessionState State { get; set; } = McpSessionState.Active;
    public Dictionary<string, object> SessionData { get; set; } = new();
    public List<string> Capabilities { get; set; } = new();
    public McpAuthenticationInfo? AuthenticationInfo { get; set; }
}

/// <summary>
/// Authentication information for a session
/// </summary>
public class McpAuthenticationInfo
{
    public string AuthenticationType { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
    public Dictionary<string, string> Claims { get; set; } = new();
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// Session state enumeration
/// </summary>
public enum McpSessionState
{
    Active,
    Expired,
    Terminated,
    Suspended
}

/// <summary>
/// Request to create a new session
/// </summary>
public class CreateSessionRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public List<string> RequestedCapabilities { get; set; } = new();
    public int? TimeoutMinutes { get; set; }
}

/// <summary>
/// Response when creating a session
/// </summary>
public class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public List<string> GrantedCapabilities { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Session validation result
/// </summary>
public class SessionValidationResult
{
    public bool IsValid { get; set; }
    public McpSession? Session { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresAuthentication { get; set; }
}

/// <summary>
/// Connection tracking information
/// </summary>
public class McpConnectionInfo
{
    public string ConnectionId { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string RemoteEndpoint { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public ConnectionState State { get; set; } = ConnectionState.Connected;
    public Dictionary<string, object> ConnectionMetadata { get; set; } = new();
}

/// <summary>
/// Connection state enumeration
/// </summary>
public enum ConnectionState
{
    Connected,
    Disconnected,
    Error,
    Timeout
}

/// <summary>
/// Session cleanup configuration
/// </summary>
public class SessionCleanupConfiguration
{
    public TimeSpan DefaultSessionTimeout { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromHours(2);
    public int MaxSessionsPerClient { get; set; } = 10;
    public bool EnableAutomaticCleanup { get; set; } = true;
}

/// <summary>
/// Request to register a connection
/// </summary>
public class ConnectionRegistrationRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}