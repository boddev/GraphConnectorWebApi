using System.Text.Json.Serialization;

namespace ApiGraphActivator.Models.Mcp;

/// <summary>
/// MCP capability information
/// </summary>
public class McpCapability
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

/// <summary>
/// MCP server capabilities
/// </summary>
public class McpServerCapabilities
{
    [JsonPropertyName("resources")]
    public McpResourceCapability? Resources { get; set; }

    [JsonPropertyName("tools")]
    public McpToolCapability? Tools { get; set; }

    [JsonPropertyName("prompts")]
    public McpPromptCapability? Prompts { get; set; }
}

/// <summary>
/// Resource capability definition
/// </summary>
public class McpResourceCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; set; }

    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Tool capability definition
/// </summary>
public class McpToolCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Prompt capability definition
/// </summary>
public class McpPromptCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// MCP client information
/// </summary>
public class McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// MCP server information
/// </summary>
public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";
}

/// <summary>
/// Initialize request parameters
/// </summary>
public class McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public McpClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// Initialize response result
/// </summary>
public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpServerCapabilities? Capabilities { get; set; }

    [JsonPropertyName("serverInfo")]
    public McpServerInfo? ServerInfo { get; set; }
}