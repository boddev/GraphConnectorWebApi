using System.Text.Json.Serialization;

namespace ApiGraphActivator.Models.Mcp;

/// <summary>
/// MCP resource definition
/// </summary>
public class McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; set; }
}

/// <summary>
/// MCP prompt definition
/// </summary>
public class McpPrompt
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("arguments")]
    public List<McpPromptArgument>? Arguments { get; set; }
}

/// <summary>
/// MCP prompt argument
/// </summary>
public class McpPromptArgument
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// List resources result
/// </summary>
public class McpListResourcesResult
{
    [JsonPropertyName("resources")]
    public List<McpResource> Resources { get; set; } = new();
}

/// <summary>
/// List tools result
/// </summary>
public class McpListToolsResult
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new();
}

/// <summary>
/// List prompts result
/// </summary>
public class McpListPromptsResult
{
    [JsonPropertyName("prompts")]
    public List<McpPrompt> Prompts { get; set; } = new();
}

/// <summary>
/// Tool call parameters
/// </summary>
public class McpCallToolParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// Tool call result
/// </summary>
public class McpCallToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// MCP content
/// </summary>
public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Read resource parameters
/// </summary>
public class McpReadResourceParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

/// <summary>
/// Read resource result
/// </summary>
public class McpReadResourceResult
{
    [JsonPropertyName("contents")]
    public List<McpResourceContent> Contents { get; set; } = new();
}

/// <summary>
/// Resource content
/// </summary>
public class McpResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("blob")]
    public string? Blob { get; set; }
}