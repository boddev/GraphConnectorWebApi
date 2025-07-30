using System.Collections.Concurrent;
using ApiGraphActivator.Models.Mcp;
using Microsoft.Extensions.Options;

namespace ApiGraphActivator.Services.Mcp;

/// <summary>
/// In-memory implementation of connection management for MCP clients
/// </summary>
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, McpConnectionInfo> _connections = new();
    private readonly ILogger<ConnectionManager> _logger;
    private readonly SessionCleanupConfiguration _config;

    public ConnectionManager(
        ILogger<ConnectionManager> logger,
        IOptions<SessionCleanupConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<string> RegisterConnectionAsync(string sessionId, string remoteEndpoint, Dictionary<string, object>? metadata = null)
    {
        await Task.CompletedTask; // Make async

        var connection = new McpConnectionInfo
        {
            SessionId = sessionId,
            RemoteEndpoint = remoteEndpoint,
            ConnectionMetadata = metadata ?? new Dictionary<string, object>()
        };

        _connections.TryAdd(connection.ConnectionId, connection);

        _logger.LogInformation("Connection {ConnectionId} registered for session {SessionId} from {RemoteEndpoint}", 
            connection.ConnectionId, sessionId, remoteEndpoint);

        return connection.ConnectionId;
    }

    public async Task UpdateConnectionActivityAsync(string connectionId)
    {
        await Task.CompletedTask; // Make async

        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.LastActivityAt = DateTime.UtcNow;
            _logger.LogTrace("Updated activity for connection {ConnectionId}", connectionId);
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        await Task.CompletedTask; // Make async

        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.State = ConnectionState.Disconnected;
            _connections.TryRemove(connectionId, out _);
            _logger.LogInformation("Connection {ConnectionId} disconnected", connectionId);
        }
    }

    public async Task<List<McpConnectionInfo>> GetSessionConnectionsAsync(string sessionId)
    {
        await Task.CompletedTask; // Make async

        return _connections.Values
            .Where(c => c.SessionId == sessionId && c.State == ConnectionState.Connected)
            .ToList();
    }

    public async Task<McpConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        await Task.CompletedTask; // Make async

        _connections.TryGetValue(connectionId, out var connection);
        return connection;
    }

    public async Task CleanupInactiveConnectionsAsync()
    {
        await Task.CompletedTask; // Make async

        var now = DateTime.UtcNow;
        var inactiveConnections = _connections.Values
            .Where(c => (now - c.LastActivityAt) > _config.InactivityTimeout)
            .ToList();

        foreach (var connection in inactiveConnections)
        {
            connection.State = ConnectionState.Timeout;
            _connections.TryRemove(connection.ConnectionId, out _);
            _logger.LogDebug("Cleaned up inactive connection {ConnectionId}", connection.ConnectionId);
        }

        if (inactiveConnections.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} inactive connections", inactiveConnections.Count);
        }
    }
}