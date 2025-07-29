using Azure.Data.Tables;

namespace ApiGraphActivator.Services;

public interface ICrawlStorageService
{
    Task InitializeAsync();
    Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url);
    Task MarkProcessedAsync(string url, bool success = true, string? errorMessage = null);
    Task<List<DocumentInfo>> GetUnprocessedAsync();
    Task<CrawlMetrics> GetCrawlMetricsAsync(string? companyName = null);
    Task<List<ProcessingError>> GetProcessingErrorsAsync(string? companyName = null);
    Task<bool> IsHealthyAsync();
    string GetStorageType();
}

public class DocumentInfo
{
    public string CompanyName { get; set; } = "";
    public string Form { get; set; } = "";
    public DateTime FilingDate { get; set; }
    public string Url { get; set; } = "";
    public bool Processed { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string Id { get; set; } = "";
}

public class CrawlMetrics
{
    public string CompanyName { get; set; } = "";
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int SuccessfulDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public int PendingDocuments => TotalDocuments - ProcessedDocuments;
    public double SuccessRate => TotalDocuments > 0 ? (double)SuccessfulDocuments / TotalDocuments * 100 : 0;
    public DateTime? LastProcessedDate { get; set; }
    public Dictionary<string, int> FormTypeCounts { get; set; } = new();
}

public class ProcessingError
{
    public string CompanyName { get; set; } = "";
    public string Form { get; set; } = "";
    public string Url { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public DateTime ErrorDate { get; set; }
}

public class OverallCrawlMetrics
{
    public int TotalCompanies { get; set; }
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int SuccessfulDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public int PendingDocuments => TotalDocuments - ProcessedDocuments;
    public double OverallSuccessRate => TotalDocuments > 0 ? (double)SuccessfulDocuments / TotalDocuments * 100 : 0;
    public DateTime? LastCrawlDate { get; set; }
    public List<CrawlMetrics> CompanyMetrics { get; set; } = new();
    public Dictionary<string, int> FormTypeCounts { get; set; } = new();
}

public class StorageConfiguration
{
    public string Provider { get; set; } = "Local"; // "Azure", "Local", "Memory"
    public string AzureConnectionString { get; set; } = "";
    public string CompanyTableName { get; set; } = "companies";
    public string ProcessedTableName { get; set; } = "processed";
    public string BlobContainerName { get; set; } = "filings";
    public string LocalDataPath { get; set; } = "./data";
    public bool AutoCreateTables { get; set; } = true;
}
