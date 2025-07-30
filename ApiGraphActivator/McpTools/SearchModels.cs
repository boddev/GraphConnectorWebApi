using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Parameters for company-based document search
/// </summary>
public class CompanySearchParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("formTypes")]
    public List<string>? FormTypes { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("includeContent")]
    public bool IncludeContent { get; set; } = false;
}

/// <summary>
/// Parameters for form type and date range filtering
/// </summary>
public class FormFilterParameters : PaginationParameters
{
    [JsonPropertyName("formTypes")]
    public List<string>? FormTypes { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("companyNames")]
    public List<string>? CompanyNames { get; set; }

    [JsonPropertyName("includeContent")]
    public bool IncludeContent { get; set; } = false;
}

/// <summary>
/// Parameters for full-text content search
/// </summary>
public class ContentSearchParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("searchText")]
    public string SearchText { get; set; } = string.Empty;

    [JsonPropertyName("companyNames")]
    public List<string>? CompanyNames { get; set; }

    [JsonPropertyName("formTypes")]
    public List<string>? FormTypes { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("exactMatch")]
    public bool ExactMatch { get; set; } = false;

    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = false;
}

/// <summary>
/// Search result item representing a document
/// </summary>
public class DocumentSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("formType")]
    public string FormType { get; set; } = string.Empty;

    [JsonPropertyName("filingDate")]
    public DateTime FilingDate { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("contentPreview")]
    public string? ContentPreview { get; set; }

    [JsonPropertyName("fullContent")]
    public string? FullContent { get; set; }

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("highlights")]
    public List<string>? Highlights { get; set; }
}

/// <summary>
/// Available form types for filtering
/// </summary>
public static class FormTypes
{
    public const string Form10K = "10-K";
    public const string Form10Q = "10-Q";
    public const string Form8K = "8-K";
    public const string Form10KA = "10-K/A";
    public const string Form10QA = "10-Q/A";
    public const string Form8KA = "8-K/A";

    public static readonly List<string> AllFormTypes = new()
    {
        Form10K, Form10Q, Form8K, Form10KA, Form10QA, Form8KA
    };

    public static bool IsValidFormType(string formType)
    {
        return AllFormTypes.Any(ft => string.Equals(ft, formType, StringComparison.OrdinalIgnoreCase));
    }
}