using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.Mocks;

/// <summary>
/// Mock implementation of GraphService for testing M365 Copilot integration
/// </summary>
public class MockGraphService : IGraphService
{
    private readonly ILogger<MockGraphService> _logger;
    private readonly Dictionary<string, object> _mockData = new();
    private bool _isAuthenticated = false;

    public MockGraphService(ILogger<MockGraphService> logger)
    {
        _logger = logger;
    }

    public Task<bool> AuthenticateAsync()
    {
        _isAuthenticated = true;
        _logger.LogInformation("Mock authentication successful");
        return Task.FromResult(true);
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_isAuthenticated);
    }

    public Task<string> GetUserDisplayNameAsync()
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");
        
        return Task.FromResult("Mock User");
    }

    public Task<Dictionary<string, object>> SearchExternalContentAsync(string query, int top = 10)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var mockResults = new Dictionary<string, object>
        {
            ["@odata.type"] = "#microsoft.graph.searchResponse",
            ["searchTerms"] = new[] { query },
            ["hitsContainers"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["hits"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["hitId"] = "mock-hit-1",
                            ["rank"] = 1,
                            ["summary"] = $"Mock search result for query: {query}",
                            ["resource"] = new Dictionary<string, object>
                            {
                                ["@odata.type"] = "#microsoft.graph.externalItem",
                                ["id"] = "mock-external-item-1",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["type"] = "text",
                                    ["value"] = $"This is mock content that matches the query: {query}"
                                },
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["title"] = $"Mock Document for {query}",
                                    ["company"] = "Mock Company Inc.",
                                    ["formType"] = "10-K",
                                    ["dateField"] = DateTime.UtcNow.ToString("O")
                                }
                            }
                        }
                    },
                    ["total"] = 1,
                    ["moreResultsAvailable"] = false
                }
            }
        };

        _logger.LogInformation("Mock Graph search executed for query: {Query}", query);
        return Task.FromResult(mockResults);
    }

    public Task<bool> CreateExternalConnectionAsync(string connectionId, string name, string description)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        _mockData[$"connection_{connectionId}"] = new
        {
            Id = connectionId,
            Name = name,
            Description = description,
            State = "ready",
            CreatedDateTime = DateTime.UtcNow
        };

        _logger.LogInformation("Mock external connection created: {ConnectionId}", connectionId);
        return Task.FromResult(true);
    }

    public Task<bool> IndexExternalItemAsync(string connectionId, string itemId, Dictionary<string, object> properties, string content)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var mockItem = new
        {
            Id = itemId,
            ConnectionId = connectionId,
            Properties = properties,
            Content = content,
            IndexedDateTime = DateTime.UtcNow
        };

        _mockData[$"item_{connectionId}_{itemId}"] = mockItem;

        _logger.LogInformation("Mock external item indexed: {ConnectionId}/{ItemId}", connectionId, itemId);
        return Task.FromResult(true);
    }

    public Task<Dictionary<string, object>?> GetExternalItemAsync(string connectionId, string itemId)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var key = $"item_{connectionId}_{itemId}";
        if (_mockData.TryGetValue(key, out var item))
        {
            return Task.FromResult<Dictionary<string, object>?>(
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    System.Text.Json.JsonSerializer.Serialize(item)));
        }

        return Task.FromResult<Dictionary<string, object>?>(null);
    }

    public Task<bool> DeleteExternalItemAsync(string connectionId, string itemId)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var key = $"item_{connectionId}_{itemId}";
        var removed = _mockData.Remove(key);

        if (removed)
        {
            _logger.LogInformation("Mock external item deleted: {ConnectionId}/{ItemId}", connectionId, itemId);
        }

        return Task.FromResult(removed);
    }

    public Task<List<Dictionary<string, object>>> GetConnectionItemsAsync(string connectionId, int top = 100)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var items = _mockData
            .Where(kvp => kvp.Key.StartsWith($"item_{connectionId}_"))
            .Take(top)
            .Select(kvp => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(kvp.Value))!)
            .ToList();

        return Task.FromResult(items);
    }

    public Task<Dictionary<string, object>> GetConnectionStatusAsync(string connectionId)
    {
        if (!_isAuthenticated)
            throw new UnauthorizedAccessException("Not authenticated");

        var connectionKey = $"connection_{connectionId}";
        if (_mockData.TryGetValue(connectionKey, out var connection))
        {
            var itemCount = _mockData.Keys.Count(k => k.StartsWith($"item_{connectionId}_"));
            
            return Task.FromResult(new Dictionary<string, object>
            {
                ["id"] = connectionId,
                ["state"] = "ready",
                ["itemsCount"] = itemCount,
                ["lastRefreshDateTime"] = DateTime.UtcNow.ToString("O")
            });
        }

        throw new InvalidOperationException($"Connection {connectionId} not found");
    }

    // Helper methods for testing
    public void SetAuthenticated(bool isAuthenticated)
    {
        _isAuthenticated = isAuthenticated;
    }

    public void ClearMockData()
    {
        _mockData.Clear();
    }

    public int GetMockDataCount()
    {
        return _mockData.Count;
    }

    public Dictionary<string, object> GetMockData()
    {
        return new Dictionary<string, object>(_mockData);
    }
}

/// <summary>
/// Interface for GraphService - defined here for testing purposes
/// In a real implementation, this would be in the main project
/// </summary>
public interface IGraphService
{
    Task<bool> AuthenticateAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string> GetUserDisplayNameAsync();
    Task<Dictionary<string, object>> SearchExternalContentAsync(string query, int top = 10);
    Task<bool> CreateExternalConnectionAsync(string connectionId, string name, string description);
    Task<bool> IndexExternalItemAsync(string connectionId, string itemId, Dictionary<string, object> properties, string content);
    Task<Dictionary<string, object>?> GetExternalItemAsync(string connectionId, string itemId);
    Task<bool> DeleteExternalItemAsync(string connectionId, string itemId);
    Task<List<Dictionary<string, object>>> GetConnectionItemsAsync(string connectionId, int top = 100);
    Task<Dictionary<string, object>> GetConnectionStatusAsync(string connectionId);
}