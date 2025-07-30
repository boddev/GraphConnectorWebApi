using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Base class for MCP (Model Context Protocol) tool definitions
/// </summary>
public abstract class McpToolBase
{
    [JsonPropertyName("name")]
    public abstract string Name { get; }

    [JsonPropertyName("description")]
    public abstract string Description { get; }

    [JsonPropertyName("inputSchema")]
    public abstract object InputSchema { get; }
}

/// <summary>
/// Standard MCP tool response wrapper
/// </summary>
public class McpToolResponse<T>
{
    [JsonPropertyName("content")]
    public T Content { get; set; } = default(T)!;

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    public static McpToolResponse<T> Success(T content, Dictionary<string, object>? metadata = null)
    {
        return new McpToolResponse<T>
        {
            Content = content,
            IsError = false,
            Metadata = metadata
        };
    }

    public static McpToolResponse<T> Error(string errorMessage)
    {
        return new McpToolResponse<T>
        {
            IsError = true,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Pagination parameters for search results
/// </summary>
public class PaginationParameters
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 1000)]
    public int PageSize { get; set; } = 50;

    [JsonIgnore]
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// Paginated search results
/// </summary>
public class PaginatedResult<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage => Page < TotalPages;

    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage => Page > 1;
}