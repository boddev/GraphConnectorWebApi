using ApiGraphActivator.Models.Mcp;
using ApiGraphActivator.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Basic tests for the MCP Tool Registry functionality
/// </summary>
public class McpToolRegistryTests
{
    private McpToolRegistryService CreateRegistryService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<StorageConfigurationService>();
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<McpToolRegistryService>>();
        return new McpToolRegistryService(logger, serviceProvider);
    }

    /// <summary>
    /// Test tool registration with metadata
    /// </summary>
    public void TestToolRegistration()
    {
        var registry = CreateRegistryService();
        
        var metadata = new McpToolMetadata
        {
            Name = "test_tool",
            Description = "A test tool",
            Category = "Test",
            ServiceType = typeof(McpToolRegistryTests),
            MethodName = "TestMethod"
        };
        
        registry.RegisterTool(metadata);
        
        var tools = registry.GetAllTools();
        if (!tools.ContainsKey("test_tool"))
        {
            throw new Exception("Tool registration failed");
        }
        
        var registeredTool = tools["test_tool"];
        if (registeredTool.Name != "test_tool" || registeredTool.Description != "A test tool")
        {
            throw new Exception("Tool metadata incorrect");
        }
        
        Console.WriteLine("✓ Tool registration test passed");
    }

    /// <summary>
    /// Test MCP tool list generation
    /// </summary>
    public void TestMcpToolsList()
    {
        var registry = CreateRegistryService();
        
        var metadata = new McpToolMetadata
        {
            Name = "test_tool",
            Description = "A test tool",
            ServiceType = typeof(McpToolRegistryTests),
            MethodName = "TestMethod"
        };
        
        metadata.Parameters.Add(new McpToolParameterMetadata
        {
            Name = "testParam",
            ParameterType = typeof(string),
            Description = "A test parameter",
            Required = true
        });
        
        registry.RegisterTool(metadata);
        
        var mcpList = registry.GetMcpToolsList();
        
        if (mcpList.Tools.Count == 0)
        {
            throw new Exception("MCP tools list is empty");
        }
        
        var tool = mcpList.Tools.First();
        if (tool.Name != "test_tool")
        {
            throw new Exception("Tool name incorrect in MCP list");
        }
        
        if (!tool.InputSchema.Properties.ContainsKey("testParam"))
        {
            throw new Exception("Tool parameter missing in MCP schema");
        }
        
        if (!tool.InputSchema.Required.Contains("testParam"))
        {
            throw new Exception("Required parameter not marked as required");
        }
        
        Console.WriteLine("✓ MCP tools list generation test passed");
    }

    /// <summary>
    /// Test parameter validation
    /// </summary>
    public void TestParameterValidation()
    {
        var registry = CreateRegistryService();
        
        var metadata = new McpToolMetadata
        {
            Name = "validation_test",
            Description = "Validation test tool",
            ServiceType = typeof(McpToolRegistryTests),
            MethodName = "TestMethod"
        };
        
        metadata.Parameters.Add(new McpToolParameterMetadata
        {
            Name = "requiredParam",
            ParameterType = typeof(string),
            Required = true
        });
        
        metadata.Parameters.Add(new McpToolParameterMetadata
        {
            Name = "optionalParam",
            ParameterType = typeof(int),
            Required = false
        });
        
        registry.RegisterTool(metadata);
        
        // Test valid parameters
        var validParams = new Dictionary<string, object>
        {
            { "requiredParam", "test value" },
            { "optionalParam", 42 }
        };
        
        var result = registry.ValidateParameters("validation_test", validParams);
        if (!result.IsValid)
        {
            throw new Exception($"Valid parameters failed validation: {string.Join(", ", result.Errors)}");
        }
        
        // Test missing required parameter
        var invalidParams = new Dictionary<string, object>
        {
            { "optionalParam", 42 }
        };
        
        result = registry.ValidateParameters("validation_test", invalidParams);
        if (result.IsValid)
        {
            throw new Exception("Missing required parameter should fail validation");
        }
        
        if (!result.Errors.Any(e => e.Contains("requiredParam")))
        {
            throw new Exception("Validation error should mention missing required parameter");
        }
        
        Console.WriteLine("✓ Parameter validation test passed");
    }

    /// <summary>
    /// Test JSON serialization of MCP tools
    /// </summary>
    public void TestJsonSerialization()
    {
        var registry = CreateRegistryService();
        
        var metadata = new McpToolMetadata
        {
            Name = "json_test",
            Description = "JSON serialization test",
            ServiceType = typeof(McpToolRegistryTests),
            MethodName = "TestMethod"
        };
        
        registry.RegisterTool(metadata);
        
        var mcpList = registry.GetMcpToolsList();
        
        try
        {
            var json = JsonSerializer.Serialize(mcpList, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            
            if (string.IsNullOrEmpty(json))
            {
                throw new Exception("JSON serialization produced empty result");
            }
            
            if (!json.Contains("json_test"))
            {
                throw new Exception("Tool name not found in serialized JSON");
            }
            
            // Verify it can be deserialized
            var deserialized = JsonSerializer.Deserialize<McpToolsListResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
            
            if (deserialized?.Tools?.Count != 1)
            {
                throw new Exception("Deserialization failed or tool count incorrect");
            }
            
        }
        catch (Exception ex)
        {
            throw new Exception($"JSON serialization failed: {ex.Message}");
        }
        
        Console.WriteLine("✓ JSON serialization test passed");
    }

    /// <summary>
    /// Run all tests
    /// </summary>
    public static void RunAllTests()
    {
        var tests = new McpToolRegistryTests();
        
        Console.WriteLine("Running MCP Tool Registry Tests...");
        Console.WriteLine();
        
        try
        {
            tests.TestToolRegistration();
            tests.TestMcpToolsList();
            tests.TestParameterValidation();
            tests.TestJsonSerialization();
            
            Console.WriteLine();
            Console.WriteLine("🎉 All MCP Tool Registry tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Simple test runner program
/// </summary>
public class TestRunner
{
    public static void Main(string[] args)
    {
        try
        {
            McpToolRegistryTests.RunAllTests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tests failed: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
