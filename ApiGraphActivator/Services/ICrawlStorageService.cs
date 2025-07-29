using Azure.Data.Tables;

namespace ApiGraphActivator.Services;

public interface ICrawlStorageService
{
    Task InitializeAsync();
    Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url);
    Task MarkProcessedAsync(string url);
    Task<List<DocumentInfo>> GetUnprocessedAsync();
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
    public string Id { get; set; } = "";
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
