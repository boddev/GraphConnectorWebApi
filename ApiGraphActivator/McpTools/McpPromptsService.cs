using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP Prompts endpoint implementation
/// </summary>
public class McpPromptsService
{
    private readonly PromptTemplateService _templateService;
    private readonly ILogger<McpPromptsService> _logger;

    public McpPromptsService(PromptTemplateService templateService, ILogger<McpPromptsService> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available prompts (MCP standard endpoint)
    /// </summary>
    public async Task<McpPromptsListResponse> GetPromptsAsync(McpPromptsListRequest? request = null)
    {
        try
        {
            var templates = _templateService.GetAllTemplates();

            // Apply cursor-based pagination if specified
            if (!string.IsNullOrEmpty(request?.Cursor))
            {
                // For simplicity, using cursor as an offset indicator
                if (int.TryParse(request.Cursor, out var offset))
                {
                    templates = templates.Skip(offset);
                }
            }

            var prompts = templates.Select(template => new McpPrompt
            {
                Name = template.Name,
                Description = template.Description,
                Arguments = template.Parameters.Select(p => new McpPromptArgument
                {
                    Name = p.Name,
                    Description = p.Description,
                    Required = p.Required
                }).ToList()
            }).ToList();

            var response = new McpPromptsListResponse
            {
                Prompts = prompts
            };

            _logger.LogDebug("Retrieved {PromptCount} prompts for MCP client", prompts.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompts list");
            throw;
        }
    }

    /// <summary>
    /// Get a specific prompt by name
    /// </summary>
    public async Task<McpPromptGetResponse> GetPromptAsync(string name, Dictionary<string, object>? arguments = null)
    {
        try
        {
            var template = _templateService.GetTemplate(name);
            if (template == null)
            {
                throw new ArgumentException($"Prompt '{name}' not found", nameof(name));
            }

            // If arguments are provided, render the template
            string content = template.Template;
            if (arguments?.Any() == true)
            {
                var renderResult = await _templateService.RenderTemplateAsync(name, arguments);
                content = renderResult.RenderedPrompt;
            }

            var response = new McpPromptGetResponse
            {
                Description = template.Description,
                Messages = new List<McpPromptMessage>
                {
                    new McpPromptMessage
                    {
                        Role = "user",
                        Content = new McpPromptContent
                        {
                            Type = "text",
                            Text = content
                        }
                    }
                }
            };

            _logger.LogDebug("Retrieved prompt '{PromptName}' with {ArgumentCount} arguments", name, arguments?.Count ?? 0);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompt '{PromptName}'", name);
            throw;
        }
    }
}

/// <summary>
/// MCP Prompts list request
/// </summary>
public class McpPromptsListRequest
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// MCP Prompts list response
/// </summary>
public class McpPromptsListResponse
{
    [JsonPropertyName("prompts")]
    public List<McpPrompt> Prompts { get; set; } = new();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// MCP Prompt definition
/// </summary>
public class McpPrompt
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("arguments")]
    public List<McpPromptArgument> Arguments { get; set; } = new();
}

/// <summary>
/// MCP Prompt argument definition
/// </summary>
public class McpPromptArgument
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// MCP Prompt get request
/// </summary>
public class McpPromptGetRequest
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// MCP Prompt get response
/// </summary>
public class McpPromptGetResponse
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("messages")]
    public List<McpPromptMessage> Messages { get; set; } = new();
}

/// <summary>
/// MCP Prompt message
/// </summary>
public class McpPromptMessage
{
    [JsonPropertyName("role")]
    [Required]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [Required]
    public McpPromptContent Content { get; set; } = null!;
}

/// <summary>
/// MCP Prompt content
/// </summary>
public class McpPromptContent
{
    [JsonPropertyName("type")]
    [Required]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}