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
    Task<Dictionary<int, YearlyMetrics>> GetYearlyMetricsAsync();
    Task<Dictionary<int, YearlyMetrics>> GetCompanyYearlyMetricsAsync(string companyName);
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

public class YearlyMetrics
{
    public int Year { get; set; }
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int SuccessfulDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public int PendingDocuments => TotalDocuments - ProcessedDocuments;
    public double SuccessRate => TotalDocuments > 0 ? (double)SuccessfulDocuments / TotalDocuments * 100 : 0;
    public Dictionary<string, int> FormTypeCounts { get; set; } = new();
    public List<string> Companies { get; set; } = new();
}

public static class DocumentIdGenerator
{
    /// <summary>
    /// Generates a reproducible ID based on the document's URL
    /// This ensures the same document always gets the same ID across crawls
    /// </summary>
    public static string GenerateDocumentId(string url)
    {
        if (string.IsNullOrEmpty(url))
            return Guid.NewGuid().ToString();

        // Use a hash of the URL to create a reproducible ID
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Generates a reproducible ID based on company name, form, and filing date
    /// This is an alternative approach that creates human-readable IDs
    /// </summary>
    public static string GenerateDocumentId(string companyName, string form, DateTime filingDate)
    {
        var cleanCompanyName = System.Text.RegularExpressions.Regex.Replace(companyName, @"[^A-Za-z0-9]", "_");
        var cleanForm = System.Text.RegularExpressions.Regex.Replace(form, @"[^A-Za-z0-9]", "_");
        var dateString = filingDate.ToString("yyyy-MM-dd");
        
        return $"{cleanCompanyName}_{cleanForm}_{dateString}".ToLowerInvariant();
    }
}
