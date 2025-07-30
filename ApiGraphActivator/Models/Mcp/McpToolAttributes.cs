namespace ApiGraphActivator.Models.Mcp;

/// <summary>
/// Attribute to mark methods as MCP tools for automatic registration
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class McpToolAttribute : Attribute
{
    /// <summary>
    /// Unique name for the MCP tool
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of what the tool does
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Category to group related tools
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Version of the tool
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Whether the tool is currently enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Attribute to describe MCP tool parameters
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class McpToolParameterAttribute : Attribute
{
    /// <summary>
    /// Description of the parameter
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Whether the parameter is required
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Default value for the parameter
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Minimum value for numeric parameters
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Maximum value for numeric parameters
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Pattern for string validation
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Format hint for string parameters
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Valid enum values for the parameter
    /// </summary>
    public string[]? EnumValues { get; set; }
}

/// <summary>
/// Metadata about a registered MCP tool
/// </summary>
public class McpToolMetadata
{
    /// <summary>
    /// Tool name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Tool description
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Category the tool belongs to
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Tool version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Whether the tool is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Type information for the method implementing the tool
    /// </summary>
    public required Type ServiceType { get; set; }

    /// <summary>
    /// Method name implementing the tool
    /// </summary>
    public required string MethodName { get; set; }

    /// <summary>
    /// Parameter metadata
    /// </summary>
    public List<McpToolParameterMetadata> Parameters { get; set; } = new();

    /// <summary>
    /// When the tool was registered
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata about a tool parameter
/// </summary>
public class McpToolParameterMetadata
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Parameter type
    /// </summary>
    public required Type ParameterType { get; set; }

    /// <summary>
    /// Parameter description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the parameter is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Default value
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Validation constraints
    /// </summary>
    public McpToolParameterConstraints? Constraints { get; set; }
}

/// <summary>
/// Validation constraints for tool parameters
/// </summary>
public class McpToolParameterConstraints
{
    /// <summary>
    /// Minimum value for numeric parameters
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Maximum value for numeric parameters
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Pattern for string validation
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Format hint
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Valid enum values
    /// </summary>
    public string[]? EnumValues { get; set; }
}