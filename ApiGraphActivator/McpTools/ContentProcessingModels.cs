using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Parameters for document summarization
/// </summary>
public class DocumentSummarizationParameters
{
    [JsonPropertyName("documentUrl")]
    public string? DocumentUrl { get; set; }

    [JsonPropertyName("documentContent")]
    public string? DocumentContent { get; set; }

    [JsonPropertyName("summaryLength")]
    [Range(1, 5)]
    public int SummaryLength { get; set; } = 3; // 1=brief, 2=short, 3=medium, 4=detailed, 5=comprehensive

    [JsonPropertyName("focusAreas")]
    public List<string>? FocusAreas { get; set; }

    [JsonPropertyName("includeKeyMetrics")]
    public bool IncludeKeyMetrics { get; set; } = true;

    [JsonPropertyName("maxPages")]
    [Range(1, 500)]
    public int MaxPages { get; set; } = 100;
}

/// <summary>
/// Result of document summarization
/// </summary>
public class DocumentSummarizationResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("keyPoints")]
    public List<string> KeyPoints { get; set; } = new();

    [JsonPropertyName("keyMetrics")]
    public Dictionary<string, string>? KeyMetrics { get; set; }

    [JsonPropertyName("documentInfo")]
    public DocumentInfo DocumentInfo { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// Parameters for key information extraction
/// </summary>
public class KeyInformationExtractionParameters
{
    [JsonPropertyName("documentUrl")]
    public string? DocumentUrl { get; set; }

    [JsonPropertyName("documentContent")]
    public string? DocumentContent { get; set; }

    [JsonPropertyName("extractionType")]
    [Required]
    public string ExtractionType { get; set; } = string.Empty; // "company", "financial", "legal", "risk", "all"

    [JsonPropertyName("maxPages")]
    [Range(1, 500)]
    public int MaxPages { get; set; } = 100;
}

/// <summary>
/// Result of key information extraction
/// </summary>
public class KeyInformationExtractionResult
{
    [JsonPropertyName("companyInfo")]
    public CompanyInformation? CompanyInfo { get; set; }

    [JsonPropertyName("financialInfo")]
    public FinancialInformation? FinancialInfo { get; set; }

    [JsonPropertyName("legalInfo")]
    public LegalInformation? LegalInfo { get; set; }

    [JsonPropertyName("riskFactors")]
    public List<string>? RiskFactors { get; set; }

    [JsonPropertyName("documentInfo")]
    public DocumentInfo DocumentInfo { get; set; } = new();

    [JsonPropertyName("extractedSections")]
    public Dictionary<string, string>? ExtractedSections { get; set; }
}

/// <summary>
/// Parameters for financial data extraction
/// </summary>
public class FinancialDataExtractionParameters
{
    [JsonPropertyName("documentUrl")]
    public string? DocumentUrl { get; set; }

    [JsonPropertyName("documentContent")]
    public string? DocumentContent { get; set; }

    [JsonPropertyName("extractionScope")]
    public List<string>? ExtractionScope { get; set; } // "revenue", "profit", "assets", "liabilities", "ratios", "all"

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("maxPages")]
    [Range(1, 500)]
    public int MaxPages { get; set; } = 100;
}

/// <summary>
/// Result of financial data extraction
/// </summary>
public class FinancialDataExtractionResult
{
    [JsonPropertyName("financialStatements")]
    public FinancialStatements? FinancialStatements { get; set; }

    [JsonPropertyName("keyMetrics")]
    public FinancialMetrics? KeyMetrics { get; set; }

    [JsonPropertyName("ratios")]
    public FinancialRatios? Ratios { get; set; }

    [JsonPropertyName("trends")]
    public List<FinancialTrend>? Trends { get; set; }

    [JsonPropertyName("documentInfo")]
    public DocumentInfo DocumentInfo { get; set; } = new();

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("reportingPeriod")]
    public string? ReportingPeriod { get; set; }
}

/// <summary>
/// Parameters for cross-document relationship analysis
/// </summary>
public class CrossDocumentRelationshipParameters
{
    [JsonPropertyName("documentUrls")]
    public List<string>? DocumentUrls { get; set; }

    [JsonPropertyName("documentContents")]
    public List<string>? DocumentContents { get; set; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("analysisType")]
    public string AnalysisType { get; set; } = "timeline"; // "timeline", "consistency", "evolution", "relationships"

    [JsonPropertyName("focusAreas")]
    public List<string>? FocusAreas { get; set; }

    [JsonPropertyName("maxPagesPerDoc")]
    [Range(1, 200)]
    public int MaxPagesPerDoc { get; set; } = 50;
}

/// <summary>
/// Result of cross-document relationship analysis
/// </summary>
public class CrossDocumentRelationshipResult
{
    [JsonPropertyName("relationships")]
    public List<DocumentRelationship> Relationships { get; set; } = new();

    [JsonPropertyName("timeline")]
    public List<TimelineEvent>? Timeline { get; set; }

    [JsonPropertyName("consistencyAnalysis")]
    public ConsistencyAnalysis? ConsistencyAnalysis { get; set; }

    [JsonPropertyName("evolutionAnalysis")]
    public EvolutionAnalysis? EvolutionAnalysis { get; set; }

    [JsonPropertyName("documentCount")]
    public int DocumentCount { get; set; }

    [JsonPropertyName("analysisType")]
    public string AnalysisType { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for PDF processing tool
/// </summary>
public class PdfProcessingParameters
{
    [JsonPropertyName("pdfUrl")]
    public string? PdfUrl { get; set; }

    [JsonPropertyName("pdfBytes")]
    public byte[]? PdfBytes { get; set; }

    [JsonPropertyName("maxPages")]
    [Range(1, 1000)]
    public int MaxPages { get; set; } = 100;

    [JsonPropertyName("includePageNumbers")]
    public bool IncludePageNumbers { get; set; } = true;

    [JsonPropertyName("cleanText")]
    public bool CleanText { get; set; } = true;
}

/// <summary>
/// Result of PDF processing
/// </summary>
public class PdfProcessingResult
{
    [JsonPropertyName("extractedText")]
    public string ExtractedText { get; set; } = string.Empty;

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("characterCount")]
    public int CharacterCount { get; set; }

    [JsonPropertyName("wordCount")]
    public int WordCount { get; set; }

    [JsonPropertyName("documentInfo")]
    public DocumentInfo DocumentInfo { get; set; } = new();
}

/// <summary>
/// Common document information
/// </summary>
public class DocumentInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("characterCount")]
    public int CharacterCount { get; set; }

    [JsonPropertyName("processingDate")]
    public DateTime ProcessingDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }
}

/// <summary>
/// Company information structure
/// </summary>
public class CompanyInformation
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("headquarters")]
    public string? Headquarters { get; set; }

    [JsonPropertyName("employeeCount")]
    public string? EmployeeCount { get; set; }

    [JsonPropertyName("businessDescription")]
    public string? BusinessDescription { get; set; }
}

/// <summary>
/// Financial information structure
/// </summary>
public class FinancialInformation
{
    [JsonPropertyName("revenue")]
    public string? Revenue { get; set; }

    [JsonPropertyName("netIncome")]
    public string? NetIncome { get; set; }

    [JsonPropertyName("totalAssets")]
    public string? TotalAssets { get; set; }

    [JsonPropertyName("totalLiabilities")]
    public string? TotalLiabilities { get; set; }

    [JsonPropertyName("shareholders")]
    public string? ShareholdersEquity { get; set; }

    [JsonPropertyName("cashAndEquivalents")]
    public string? CashAndEquivalents { get; set; }
}

/// <summary>
/// Legal information structure
/// </summary>
public class LegalInformation
{
    [JsonPropertyName("legalProceedings")]
    public List<string>? LegalProceedings { get; set; }

    [JsonPropertyName("regulations")]
    public List<string>? Regulations { get; set; }

    [JsonPropertyName("compliance")]
    public string? Compliance { get; set; }

    [JsonPropertyName("corporateGovernance")]
    public string? CorporateGovernance { get; set; }
}

/// <summary>
/// Financial statements structure
/// </summary>
public class FinancialStatements
{
    [JsonPropertyName("incomeStatement")]
    public Dictionary<string, string>? IncomeStatement { get; set; }

    [JsonPropertyName("balanceSheet")]
    public Dictionary<string, string>? BalanceSheet { get; set; }

    [JsonPropertyName("cashFlowStatement")]
    public Dictionary<string, string>? CashFlowStatement { get; set; }
}

/// <summary>
/// Financial metrics structure
/// </summary>
public class FinancialMetrics
{
    [JsonPropertyName("revenue")]
    public string? Revenue { get; set; }

    [JsonPropertyName("grossProfit")]
    public string? GrossProfit { get; set; }

    [JsonPropertyName("operatingIncome")]
    public string? OperatingIncome { get; set; }

    [JsonPropertyName("netIncome")]
    public string? NetIncome { get; set; }

    [JsonPropertyName("eps")]
    public string? EarningsPerShare { get; set; }

    [JsonPropertyName("totalAssets")]
    public string? TotalAssets { get; set; }

    [JsonPropertyName("totalDebt")]
    public string? TotalDebt { get; set; }

    [JsonPropertyName("freeCashFlow")]
    public string? FreeCashFlow { get; set; }
}

/// <summary>
/// Financial ratios structure
/// </summary>
public class FinancialRatios
{
    [JsonPropertyName("currentRatio")]
    public string? CurrentRatio { get; set; }

    [JsonPropertyName("debtToEquity")]
    public string? DebtToEquity { get; set; }

    [JsonPropertyName("returnOnAssets")]
    public string? ReturnOnAssets { get; set; }

    [JsonPropertyName("returnOnEquity")]
    public string? ReturnOnEquity { get; set; }

    [JsonPropertyName("profitMargin")]
    public string? ProfitMargin { get; set; }

    [JsonPropertyName("grossMargin")]
    public string? GrossMargin { get; set; }
}

/// <summary>
/// Financial trend structure
/// </summary>
public class FinancialTrend
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty; // "increasing", "decreasing", "stable"

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("significance")]
    public string Significance { get; set; } = string.Empty; // "high", "medium", "low"
}

/// <summary>
/// Document relationship structure
/// </summary>
public class DocumentRelationship
{
    [JsonPropertyName("sourceDocument")]
    public string SourceDocument { get; set; } = string.Empty;

    [JsonPropertyName("targetDocument")]
    public string TargetDocument { get; set; } = string.Empty;

    [JsonPropertyName("relationshipType")]
    public string RelationshipType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// Timeline event structure
/// </summary>
public class TimelineEvent
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sourceDocument")]
    public string SourceDocument { get; set; } = string.Empty;

    [JsonPropertyName("importance")]
    public string Importance { get; set; } = string.Empty; // "high", "medium", "low"
}

/// <summary>
/// Consistency analysis structure
/// </summary>
public class ConsistencyAnalysis
{
    [JsonPropertyName("overallConsistency")]
    public string OverallConsistency { get; set; } = string.Empty; // "high", "medium", "low"

    [JsonPropertyName("inconsistencies")]
    public List<string>? Inconsistencies { get; set; }

    [JsonPropertyName("consistentThemes")]
    public List<string>? ConsistentThemes { get; set; }

    [JsonPropertyName("analysisNotes")]
    public string? AnalysisNotes { get; set; }
}

/// <summary>
/// Evolution analysis structure
/// </summary>
public class EvolutionAnalysis
{
    [JsonPropertyName("keyChanges")]
    public List<string>? KeyChanges { get; set; }

    [JsonPropertyName("evolutionTrends")]
    public List<string>? EvolutionTrends { get; set; }

    [JsonPropertyName("significantDevelopments")]
    public List<string>? SignificantDevelopments { get; set; }

    [JsonPropertyName("analysisNotes")]
    public string? AnalysisNotes { get; set; }
}