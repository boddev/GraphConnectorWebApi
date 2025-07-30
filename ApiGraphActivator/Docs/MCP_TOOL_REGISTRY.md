# MCP Tool Registry

## Overview

The MCP (Model Context Protocol) Tool Registry provides a system for registering and exposing API capabilities as discoverable tools that can be used by AI models and other clients following the MCP specification.

## Features

- **Automatic Tool Discovery**: Uses reflection and attributes to automatically discover tools from assemblies
- **Schema Definition**: Generates JSON schemas for tool parameters with validation
- **Parameter Validation**: Validates tool parameters against defined schemas
- **RESTful API**: Exposes tools via standard HTTP endpoints
- **OpenAPI Integration**: Tools are documented in Swagger/OpenAPI specifications

## Architecture

### Core Components

1. **Models** (`Models/Mcp/`)
   - `McpToolSchema.cs`: Core MCP tool schema models
   - `McpToolAttributes.cs`: Attributes for marking methods as tools

2. **Services** (`Services/`)
   - `McpToolRegistryService.cs`: Core registry functionality
   - `McpToolsService.cs`: Example tools implementation

### Tool Registration

Tools are registered using attributes:

```csharp
[McpTool(
    Name = "get_company_info",
    Description = "Retrieve SEC company information",
    Category = "Edgar Data"
)]
public async Task<object> GetCompanyInfo(
    [McpToolParameter(Description = "Company ticker or name", Required = true)]
    string query)
{
    // Implementation
}
```

## API Endpoints

### `/tools/list` (GET)
Returns all registered tools in MCP format.

**Response:**
```json
{
  "tools": [
    {
      "name": "get_company_info",
      "description": "Retrieve SEC company information",
      "inputSchema": {
        "type": "object",
        "properties": {
          "query": {
            "type": "string",
            "description": "Company ticker symbol or name",
            "default": null
          }
        },
        "required": ["query"],
        "additionalProperties": false
      }
    }
  ]
}
```

### `/tools/registry/status` (GET)
Returns registry status and statistics.

**Response:**
```json
{
  "totalTools": 6,
  "enabledTools": 6,
  "toolsByCategory": {
    "Edgar Data": 2,
    "Monitoring": 2,
    "Operations": 1,
    "Configuration": 1
  },
  "registeredAt": "2025-01-01T00:00:00Z"
}
```

### `/tools/validate` (POST)
Validates tool parameters against schemas.

**Request:**
```json
{
  "toolName": "get_company_info",
  "parameters": {
    "query": "AAPL"
  }
}
```

**Response:**
```json
{
  "isValid": true,
  "errors": []
}
```

## Available Tools

The following tools are automatically registered:

### Edgar Data Tools
- **`get_company_info`**: Retrieve SEC company information by ticker or name
- **`get_crawled_companies`**: Get previously crawled companies information

### Monitoring Tools
- **`get_crawl_status`**: Get comprehensive crawl status and metrics
- **`get_company_metrics`**: Get detailed metrics for specific companies

### Operations Tools
- **`start_crawl`**: Initiate background crawl process for companies

### Configuration Tools
- **`get_storage_config`**: Retrieve storage configuration and health status

## Usage Examples

### Registering a New Tool

```csharp
public class MyService
{
    [McpTool(
        Name = "my_tool",
        Description = "Does something useful",
        Category = "Utilities"
    )]
    public async Task<object> MyTool(
        [McpToolParameter(Description = "Input parameter", Required = true)]
        string input,
        [McpToolParameter(Description = "Optional setting", Required = false)]
        bool? option = null)
    {
        return new { result = $"Processed: {input}" };
    }
}
```

### Using the Registry Programmatically

```csharp
// Get registry instance
var registry = serviceProvider.GetRequiredService<McpToolRegistryService>();

// Get all tools
var tools = registry.GetAllTools();

// Get MCP-formatted tools list
var mcpList = registry.GetMcpToolsList();

// Validate parameters
var validation = registry.ValidateParameters("my_tool", parameters);
```

## Configuration

The tool registry is automatically configured in `Program.cs`:

```csharp
// Register services
builder.Services.AddSingleton<McpToolRegistryService>();
builder.Services.AddSingleton<McpToolsService>();

// Initialize after app is built
var mcpToolRegistry = app.Services.GetRequiredService<McpToolRegistryService>();
mcpToolRegistry.DiscoverTools();
```

## Parameter Types

Supported parameter types and their JSON schema mappings:

| .NET Type | JSON Schema Type |
|-----------|------------------|
| `string` | `string` |
| `int`, `long`, `short` | `integer` |
| `double`, `float`, `decimal` | `number` |
| `bool` | `boolean` |
| Arrays/Lists | `array` |
| Other objects | `object` |

## Parameter Validation

Parameters can have constraints:

```csharp
[McpToolParameter(
    Description = "Age parameter",
    Required = true,
    Minimum = 0,
    Maximum = 150
)]
int age
```

Available constraints:
- `Required`: Whether the parameter is mandatory
- `Minimum`/`Maximum`: Numeric range constraints
- `Pattern`: Regex pattern for string validation
- `Format`: Format hint (e.g., "email", "uri")
- `EnumValues`: Valid enumeration values

## Testing

The implementation includes comprehensive tests in `Tests/McpTests/`:

```bash
cd Tests/McpTests
dotnet run
```

Tests cover:
- Tool registration and metadata
- MCP tools list generation
- Parameter validation
- JSON serialization
- Discovery robustness

## Extending the Registry

To add new tools:

1. Create a service class with methods marked with `[McpTool]`
2. Register the service in DI container
3. The registry will automatically discover tools on startup

## Troubleshooting

### Common Issues

**Tool not discovered:**
- Ensure method is public
- Verify `[McpTool]` attribute is present
- Check that the containing class is instantiated or marked as static

**Parameter validation fails:**
- Verify parameter names match exactly
- Check required parameters are provided
- Ensure parameter types match expected JSON types

**JSON serialization issues:**
- Ensure all return types are serializable
- Use proper JsonPropertyName attributes if needed
- Check for circular references in object graphs

## Integration with MCP Clients

The registry follows the MCP specification for tool discovery, making it compatible with:
- MCP-compliant AI models
- Claude Desktop and other MCP clients
- Custom applications using the MCP protocol

Tools can be invoked by MCP clients using the standard tool calling mechanism with the schemas provided by `/tools/list`.