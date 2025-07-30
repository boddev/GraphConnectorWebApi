using System.Text.Json.Serialization;

namespace ApiGraphActivator.Models.Mcp;

/// <summary>
/// Represents an MCP tool definition according to the MCP specification
/// </summary>
public class McpTool
{
    /// <summary>
    /// Unique identifier for the tool
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// JSON schema defining the tool's input parameters
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public required McpToolInputSchema InputSchema { get; set; }
}

/// <summary>
/// JSON schema for tool input parameters
/// </summary>
public class McpToolInputSchema
{
    /// <summary>
    /// Schema type (always "object" for tool parameters)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    /// <summary>
    /// Schema properties defining individual parameters
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, McpToolParameter> Properties { get; set; } = new();

    /// <summary>
    /// List of required parameter names
    /// </summary>
    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();

    /// <summary>
    /// Additional properties allowed
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; set; } = false;
}

/// <summary>
/// Definition of a tool parameter
/// </summary>
public class McpToolParameter
{
    /// <summary>
    /// Parameter data type
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable description of the parameter
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Default value for the parameter
    /// </summary>
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>
    /// Enum values for string parameters
    /// </summary>
    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    /// <summary>
    /// Minimum value for numeric parameters
    /// </summary>
    [JsonPropertyName("minimum")]
    public double? Minimum { get; set; }

    /// <summary>
    /// Maximum value for numeric parameters
    /// </summary>
    [JsonPropertyName("maximum")]
    public double? Maximum { get; set; }

    /// <summary>
    /// Pattern for string validation
    /// </summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>
    /// Format hint for string parameters
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

/// <summary>
/// Response structure for the MCP tools/list endpoint
/// </summary>
public class McpToolsListResponse
{
    /// <summary>
    /// List of available tools
    /// </summary>
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new();
}