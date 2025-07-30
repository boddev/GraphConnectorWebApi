using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Parameters for document summarization
/// </summary>
public class DocumentSummarizationParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("documentIds")]
    public List<string> DocumentIds { get; set; } = new();

    [JsonPropertyName("summaryType")]
    public string SummaryType { get; set; } = "comprehensive"; // comprehensive, executive, financial, risks

    [JsonPropertyName("includeMetrics")]
    public bool IncludeMetrics { get; set; } = true;

    [JsonPropertyName("includeRisks")]
    public bool IncludeRisks { get; set; } = true;
}

/// <summary>
/// Parameters for document question answering
/// </summary>
public class DocumentQuestionAnswerParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [Required]
    [JsonPropertyName("documentIds")]
    public List<string> DocumentIds { get; set; } = new();

    [JsonPropertyName("includeContext")]
    public bool IncludeContext { get; set; } = true;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 4000;
}

/// <summary>
/// Parameters for document comparison
/// </summary>
public class DocumentComparisonParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("documentIds")]
    [MinLength(2, ErrorMessage = "At least 2 documents are required for comparison")]
    public List<string> DocumentIds { get; set; } = new();

    [JsonPropertyName("comparisonType")]
    public string ComparisonType { get; set; } = "comprehensive"; // comprehensive, financial, operational, strategic

    [JsonPropertyName("focusAreas")]
    public List<string> FocusAreas { get; set; } = new(); // revenue, margins, risks, strategy, etc.
}

/// <summary>
/// Parameters for financial analysis
/// </summary>
public class FinancialAnalysisParameters : PaginationParameters
{
    [Required]
    [JsonPropertyName("documentIds")]
    public List<string> DocumentIds { get; set; } = new();

    [JsonPropertyName("analysisType")]
    public string AnalysisType { get; set; } = "comprehensive"; // comprehensive, performance, position, ratios, trends

    [JsonPropertyName("includeProjections")]
    public bool IncludeProjections { get; set; } = true;

    [JsonPropertyName("includeRatios")]
    public bool IncludeRatios { get; set; } = true;

    [JsonPropertyName("timeFrame")]
    public string TimeFrame { get; set; } = "current"; // current, historical, comparative
}

/// <summary>
/// Analysis result with structured information
/// </summary>
public class AnalysisResultData
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("keyFindings")]
    public List<string> KeyFindings { get; set; } = new();

    [JsonPropertyName("metrics")]
    public Dictionary<string, object> Metrics { get; set; } = new();

    [JsonPropertyName("insights")]
    public List<AnalysisInsight> Insights { get; set; } = new();

    [JsonPropertyName("citations")]
    public List<DocumentCitation> Citations { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("analysisType")]
    public string AnalysisType { get; set; } = "";

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tokenUsage")]
    public int TokenUsage { get; set; }
}

/// <summary>
/// Individual analysis insight
/// </summary>
public class AnalysisInsight
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("insight")]
    public string Insight { get; set; } = "";

    [JsonPropertyName("importance")]
    public string Importance { get; set; } = "medium"; // high, medium, low

    [JsonPropertyName("supportingData")]
    public List<string> SupportingData { get; set; } = new();

    [JsonPropertyName("documentSources")]
    public List<string> DocumentSources { get; set; } = new();
}

/// <summary>
/// Comparison result for multiple documents
/// </summary>
public class ComparisonResultData
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("keyDifferences")]
    public List<ComparisonDifference> KeyDifferences { get; set; } = new();

    [JsonPropertyName("similarities")]
    public List<string> Similarities { get; set; } = new();

    [JsonPropertyName("trends")]
    public List<ComparisonTrend> Trends { get; set; } = new();

    [JsonPropertyName("documentsAnalyzed")]
    public List<DocumentSummary> DocumentsAnalyzed { get; set; } = new();

    [JsonPropertyName("citations")]
    public List<DocumentCitation> Citations { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Difference found in comparison
/// </summary>
public class ComparisonDifference
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("significance")]
    public string Significance { get; set; } = "medium"; // high, medium, low

    [JsonPropertyName("documentA")]
    public DocumentReference DocumentA { get; set; } = new();

    [JsonPropertyName("documentB")]
    public DocumentReference DocumentB { get; set; } = new();
}

/// <summary>
/// Trend identified across documents
/// </summary>
public class ComparisonTrend
{
    [JsonPropertyName("trendType")]
    public string TrendType { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = ""; // increasing, decreasing, stable, volatile

    [JsonPropertyName("dataPoints")]
    public List<TrendDataPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// Data point in a trend
/// </summary>
public class TrendDataPoint
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("metric")]
    public string Metric { get; set; } = "";
}

/// <summary>
/// Document reference with value
/// </summary>
public class DocumentReference
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("context")]
    public string Context { get; set; } = "";
}

/// <summary>
/// Summary of analyzed document
/// </summary>
public class DocumentSummary
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = "";

    [JsonPropertyName("formType")]
    public string FormType { get; set; } = "";

    [JsonPropertyName("filingDate")]
    public DateTime FilingDate { get; set; }

    [JsonPropertyName("keyMetrics")]
    public Dictionary<string, object> KeyMetrics { get; set; } = new();
}