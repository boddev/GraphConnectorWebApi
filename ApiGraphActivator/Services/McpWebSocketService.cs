using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ApiGraphActivator.Services;

/// <summary>
/// WebSocket service for MCP protocol communication
/// </summary>
public class McpWebSocketService
{
    private readonly ILogger<McpWebSocketService> _logger;
    private readonly McpProtocolHandler _protocolHandler;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;

    public McpWebSocketService(ILogger<McpWebSocketService> logger, McpProtocolHandler protocolHandler)
    {
        _logger = logger;
        _protocolHandler = protocolHandler;
        _connections = new ConcurrentDictionary<string, WebSocketConnection>();
    }

    /// <summary>
    /// Handle new WebSocket connection
    /// </summary>
    public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
    {
        var connectionId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection(connectionId, webSocket);
        
        _connections.TryAdd(connectionId, connection);
        _logger.LogInformation("New MCP WebSocket connection established: {ConnectionId}", connectionId);

        try
        {
            await ProcessWebSocketMessagesAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection {ConnectionId}", connectionId);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            _logger.LogInformation("MCP WebSocket connection closed: {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Process messages from WebSocket connection
    /// </summary>
    private async Task ProcessWebSocketMessagesAsync(WebSocketConnection connection)
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        while (connection.WebSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await connection.WebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuffer.Append(messageChunk);

                    // Check if message is complete
                    if (result.EndOfMessage)
                    {
                        var message = messageBuffer.ToString();
                        messageBuffer.Clear();

                        _logger.LogDebug("Received MCP message on connection {ConnectionId}: {Message}", 
                            connection.Id, message);

                        // Process the message through the protocol handler
                        var response = await _protocolHandler.ProcessMessageAsync(message);

                        // Send response if there is one (requests get responses, notifications don't)
                        if (!string.IsNullOrEmpty(response))
                        {
                            await SendMessageAsync(connection, response);
                        }
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("WebSocket connection {ConnectionId} closed prematurely", connection.Id);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message on connection {ConnectionId}", connection.Id);
                
                // Send error response for JSON-RPC compliance
                var errorResponse = CreateErrorResponse("Invalid message format");
                await SendMessageAsync(connection, errorResponse);
            }
        }
    }

    /// <summary>
    /// Send message to WebSocket connection
    /// </summary>
    private async Task SendMessageAsync(WebSocketConnection connection, string message)
    {
        if (connection.WebSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Attempted to send message to closed WebSocket connection {ConnectionId}", connection.Id);
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await connection.WebSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            _logger.LogDebug("Sent MCP response on connection {ConnectionId}: {Message}", 
                connection.Id, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to WebSocket connection {ConnectionId}", connection.Id);
        }
    }

    /// <summary>
    /// Broadcast message to all connected clients
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        var tasks = _connections.Values
            .Where(conn => conn.WebSocket.State == WebSocketState.Open)
            .Select(conn => SendMessageAsync(conn, message));

        await Task.WhenAll(tasks);
        _logger.LogDebug("Broadcasted message to {ConnectionCount} connections", _connections.Count);
    }

    /// <summary>
    /// Send notification to all connected clients
    /// </summary>
    public async Task SendNotificationAsync(string method, object? parameters = null)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = parameters
        };

        var message = JsonSerializer.Serialize(notification, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await BroadcastAsync(message);
    }

    /// <summary>
    /// Create error response for invalid messages
    /// </summary>
    private string CreateErrorResponse(string errorMessage)
    {
        var errorResponse = new
        {
            jsonrpc = "2.0",
            id = (object?)null,
            error = new
            {
                code = -32600,
                message = errorMessage
            }
        };

        return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Get count of active connections
    /// </summary>
    public int ActiveConnectionCount => _connections.Count(kvp => kvp.Value.WebSocket.State == WebSocketState.Open);

    /// <summary>
    /// Get connection information
    /// </summary>
    public IEnumerable<object> GetConnectionInfo()
    {
        return _connections.Values.Select(conn => new
        {
            Id = conn.Id,
            State = conn.WebSocket.State.ToString(),
            ConnectedAt = conn.ConnectedAt
        });
    }
}

/// <summary>
/// Represents a WebSocket connection for MCP protocol
/// </summary>
public class WebSocketConnection
{
    public string Id { get; }
    public WebSocket WebSocket { get; }
    public DateTime ConnectedAt { get; }

    public WebSocketConnection(string id, WebSocket webSocket)
    {
        Id = id;
        WebSocket = webSocket;
        ConnectedAt = DateTime.UtcNow;
    }
}