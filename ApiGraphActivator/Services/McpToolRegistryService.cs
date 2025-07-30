using ApiGraphActivator.Models.Mcp;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace ApiGraphActivator.Services;

/// <summary>
/// Registry service for managing MCP tools
/// </summary>
public class McpToolRegistryService
{
    private readonly ConcurrentDictionary<string, McpToolMetadata> _tools = new();
    private readonly ILogger<McpToolRegistryService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public McpToolRegistryService(ILogger<McpToolRegistryService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Register a tool manually
    /// </summary>
    public void RegisterTool(McpToolMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.Name))
        {
            throw new ArgumentException("Tool name cannot be null or empty", nameof(metadata));
        }

        if (_tools.TryAdd(metadata.Name, metadata))
        {
            _logger.LogInformation("Registered MCP tool: {ToolName} (Category: {Category})", 
                metadata.Name, metadata.Category ?? "General");
        }
        else
        {
            _logger.LogWarning("Tool {ToolName} is already registered", metadata.Name);
        }
    }

    /// <summary>
    /// Unregister a tool
    /// </summary>
    public bool UnregisterTool(string toolName)
    {
        if (_tools.TryRemove(toolName, out var metadata))
        {
            _logger.LogInformation("Unregistered MCP tool: {ToolName}", toolName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public IReadOnlyDictionary<string, McpToolMetadata> GetAllTools()
    {
        return _tools.Where(kvp => kvp.Value.Enabled)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Get tool by name
    /// </summary>
    public McpToolMetadata? GetTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool?.Enabled == true ? tool : null;
    }

    /// <summary>
    /// Auto-discover and register tools from assemblies using attributes
    /// </summary>
    public void DiscoverTools()
    {
        _logger.LogInformation("Starting MCP tool discovery...");

        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        int discoveredCount = 0;

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    var toolAttribute = method.GetCustomAttribute<McpToolAttribute>();
                    if (toolAttribute != null)
                    {
                        try
                        {
                            var metadata = CreateToolMetadata(type, method, toolAttribute);
                            RegisterTool(metadata);
                            discoveredCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to register tool {ToolName} from method {MethodName}", 
                                toolAttribute.Name, $"{type.Name}.{method.Name}");
                        }
                    }
                }
            }
        }

        _logger.LogInformation("MCP tool discovery completed. Discovered {Count} tools", discoveredCount);
    }

    /// <summary>
    /// Convert registered tools to MCP tool format
    /// </summary>
    public McpToolsListResponse GetMcpToolsList()
    {
        var tools = new List<McpTool>();

        foreach (var metadata in GetAllTools().Values)
        {
            try
            {
                var mcpTool = ConvertToMcpTool(metadata);
                tools.Add(mcpTool);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert tool {ToolName} to MCP format", metadata.Name);
            }
        }

        return new McpToolsListResponse { Tools = tools };
    }

    /// <summary>
    /// Validate tool parameters against schema
    /// </summary>
    public ValidationResult ValidateParameters(string toolName, Dictionary<string, object> parameters)
    {
        var tool = GetTool(toolName);
        if (tool == null)
        {
            return new ValidationResult { IsValid = false, Errors = { $"Tool '{toolName}' not found" } };
        }

        var result = new ValidationResult { IsValid = true };

        // Check required parameters
        foreach (var param in tool.Parameters.Where(p => p.Required))
        {
            if (!parameters.ContainsKey(param.Name))
            {
                result.Errors.Add($"Required parameter '{param.Name}' is missing");
                result.IsValid = false;
            }
        }

        // Validate parameter types and constraints
        foreach (var kvp in parameters)
        {
            var paramMetadata = tool.Parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (paramMetadata == null)
            {
                result.Errors.Add($"Unknown parameter '{kvp.Key}'");
                result.IsValid = false;
                continue;
            }

            var validationError = ValidateParameterValue(paramMetadata, kvp.Value);
            if (validationError != null)
            {
                result.Errors.Add($"Parameter '{kvp.Key}': {validationError}");
                result.IsValid = false;
            }
        }

        return result;
    }

    private McpToolMetadata CreateToolMetadata(Type type, MethodInfo method, McpToolAttribute toolAttribute)
    {
        var metadata = new McpToolMetadata
        {
            Name = toolAttribute.Name,
            Description = toolAttribute.Description,
            Category = toolAttribute.Category,
            Version = toolAttribute.Version,
            Enabled = toolAttribute.Enabled,
            ServiceType = type,
            MethodName = method.Name
        };

        // Process method parameters
        foreach (var param in method.GetParameters())
        {
            var paramAttribute = param.GetCustomAttribute<McpToolParameterAttribute>();
            var paramMetadata = new McpToolParameterMetadata
            {
                Name = param.Name ?? "unknown",
                ParameterType = param.ParameterType,
                Description = paramAttribute?.Description,
                Required = paramAttribute?.Required ?? !param.HasDefaultValue,
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : paramAttribute?.DefaultValue
            };

            if (paramAttribute != null)
            {
                paramMetadata.Constraints = new McpToolParameterConstraints
                {
                    Minimum = paramAttribute.Minimum,
                    Maximum = paramAttribute.Maximum,
                    Pattern = paramAttribute.Pattern,
                    Format = paramAttribute.Format,
                    EnumValues = paramAttribute.EnumValues
                };
            }

            metadata.Parameters.Add(paramMetadata);
        }

        return metadata;
    }

    private McpTool ConvertToMcpTool(McpToolMetadata metadata)
    {
        var inputSchema = new McpToolInputSchema();

        foreach (var param in metadata.Parameters)
        {
            var jsonType = GetJsonType(param.ParameterType);
            var paramSchema = new McpToolParameter
            {
                Type = jsonType,
                Description = param.Description,
                Default = param.DefaultValue
            };

            if (param.Constraints != null)
            {
                paramSchema.Minimum = param.Constraints.Minimum;
                paramSchema.Maximum = param.Constraints.Maximum;
                paramSchema.Pattern = param.Constraints.Pattern;
                paramSchema.Format = param.Constraints.Format;
                if (param.Constraints.EnumValues != null)
                {
                    paramSchema.Enum = param.Constraints.EnumValues.ToList();
                }
            }

            inputSchema.Properties[param.Name] = paramSchema;

            if (param.Required)
            {
                inputSchema.Required.Add(param.Name);
            }
        }

        return new McpTool
        {
            Name = metadata.Name,
            Description = metadata.Description,
            InputSchema = inputSchema
        };
    }

    private static string GetJsonType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        
        // Handle nullable types
        if (Nullable.GetUnderlyingType(type) != null)
        {
            return GetJsonType(Nullable.GetUnderlyingType(type)!);
        }

        return "object";
    }

    private static string? ValidateParameterValue(McpToolParameterMetadata param, object value)
    {
        if (value == null)
        {
            return param.Required ? "Value is required" : null;
        }

        // Type validation would be more complex in a real implementation
        // For now, just basic constraint validation
        if (param.Constraints?.EnumValues != null)
        {
            var stringValue = value.ToString();
            if (!param.Constraints.EnumValues.Contains(stringValue))
            {
                return $"Value must be one of: {string.Join(", ", param.Constraints.EnumValues)}";
            }
        }

        if (param.Constraints?.Minimum.HasValue == true && value is IComparable comparable)
        {
            if (comparable.CompareTo(param.Constraints.Minimum.Value) < 0)
            {
                return $"Value must be at least {param.Constraints.Minimum.Value}";
            }
        }

        if (param.Constraints?.Maximum.HasValue == true && value is IComparable comparable2)
        {
            if (comparable2.CompareTo(param.Constraints.Maximum.Value) > 0)
            {
                return $"Value must be at most {param.Constraints.Maximum.Value}";
            }
        }

        return null;
    }
}

/// <summary>
/// Result of parameter validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();
}