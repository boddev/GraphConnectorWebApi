using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Represents a reusable prompt template for AI interactions
/// </summary>
public class PromptTemplate
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("template")]
    [Required]
    public string Template { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [Required]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<PromptParameter> Parameters { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "System";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("examples")]
    public List<PromptExample>? Examples { get; set; }
}

/// <summary>
/// Represents a parameter that can be used in a prompt template
/// </summary>
public class PromptParameter
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [Required]
    public PromptParameterType Type { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }

    [JsonPropertyName("validation")]
    public PromptParameterValidation? Validation { get; set; }
}

/// <summary>
/// Types of prompt parameters
/// </summary>
public enum PromptParameterType
{
    String,
    Number,
    Boolean,
    Date,
    Array,
    Object
}

/// <summary>
/// Validation rules for prompt parameters
/// </summary>
public class PromptParameterValidation
{
    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("minimum")]
    public double? Minimum { get; set; }

    [JsonPropertyName("maximum")]
    public double? Maximum { get; set; }
}

/// <summary>
/// Example usage of a prompt template
/// </summary>
public class PromptExample
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    [Required]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("expectedOutput")]
    public string? ExpectedOutput { get; set; }
}

/// <summary>
/// Request model for rendering a prompt template
/// </summary>
public class RenderPromptRequest
{
    [JsonPropertyName("templateName")]
    [Required]
    public string TemplateName { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Response model for rendered prompt
/// </summary>
public class RenderPromptResponse
{
    [JsonPropertyName("renderedPrompt")]
    public string RenderedPrompt { get; set; } = string.Empty;

    [JsonPropertyName("template")]
    public PromptTemplate Template { get; set; } = null!;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Response model for prompt validation
/// </summary>
public class PromptValidationResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Categories for organizing prompt templates
/// </summary>
public static class PromptCategories
{
    public const string DocumentAnalysis = "document-analysis";
    public const string FinancialExtraction = "financial-extraction";
    public const string ComparisonSummarization = "comparison-summarization";
    public const string DataEnhancement = "data-enhancement";
    public const string ReportGeneration = "report-generation";
    public const string ComplianceCheck = "compliance-check";
}