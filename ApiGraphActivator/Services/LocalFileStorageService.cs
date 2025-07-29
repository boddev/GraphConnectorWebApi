using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class LocalFileStorageService : ICrawlStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _dataPath;
    private readonly string _documentsFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger, StorageConfiguration config)
    {
        _logger = logger;
        _dataPath = config.LocalDataPath;
        _documentsFile = Path.Combine(_dataPath, "tracked-documents.json");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogInformation("Created local storage directory: {DataPath}", _dataPath);
            }

            if (!File.Exists(_documentsFile))
            {
                await File.WriteAllTextAsync(_documentsFile, "[]");
                _logger.LogInformation("Created documents tracking file: {DocumentsFile}", _documentsFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize local file storage");
            throw;
        }
    }

    public async Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url)
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            
            // Check if document already exists
            if (documents.Any(d => d.Url == url))
            {
                _logger.LogTrace("Document already tracked: {Url}", url);
                return;
            }

            var newDoc = new DocumentInfo
            {
                Id = Guid.NewGuid().ToString(),
                CompanyName = companyName,
                Form = form,
                FilingDate = filingDate,
                Url = url,
                Processed = false
            };

            documents.Add(newDoc);
            await SaveDocumentsAsync(documents);
            _logger.LogTrace("Tracked new document: {CompanyName} {Form} {FilingDate}", companyName, form, filingDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track document: {Url}", url);
        }
    }

    public async Task MarkProcessedAsync(string url, bool success = true, string? errorMessage = null)
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            var document = documents.FirstOrDefault(d => d.Url == url);
            
            if (document != null)
            {
                document.Processed = true;
                document.ProcessedDate = DateTime.UtcNow;
                document.Success = success;
                document.ErrorMessage = errorMessage;
                await SaveDocumentsAsync(documents);
                _logger.LogTrace("Marked document as processed: {Url} - Success: {Success}", url, success);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark document as processed: {Url}", url);
        }
    }

    public async Task<List<DocumentInfo>> GetUnprocessedAsync()
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            return documents.Where(d => !d.Processed).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unprocessed documents");
            return new List<DocumentInfo>();
        }
    }

    public async Task<CrawlMetrics> GetCrawlMetricsAsync(string? companyName = null)
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            
            if (!string.IsNullOrEmpty(companyName))
            {
                documents = documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var metrics = new CrawlMetrics
            {
                CompanyName = companyName ?? "All Companies",
                TotalDocuments = documents.Count,
                ProcessedDocuments = documents.Count(d => d.Processed),
                SuccessfulDocuments = documents.Count(d => d.Processed && d.Success),
                FailedDocuments = documents.Count(d => d.Processed && !d.Success),
                LastProcessedDate = documents.Where(d => d.ProcessedDate.HasValue).Max(d => d.ProcessedDate),
                FormTypeCounts = documents.GroupBy(d => d.Form).ToDictionary(g => g.Key, g => g.Count())
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get crawl metrics for company: {Company}", companyName);
            throw;
        }
    }

    public async Task<List<ProcessingError>> GetProcessingErrorsAsync(string? companyName = null)
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            var errorDocs = documents.Where(d => d.Processed && !d.Success && !string.IsNullOrEmpty(d.ErrorMessage));
            
            if (!string.IsNullOrEmpty(companyName))
            {
                errorDocs = errorDocs.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
            }

            return errorDocs.Select(d => new ProcessingError
            {
                CompanyName = d.CompanyName,
                Form = d.Form,
                Url = d.Url,
                ErrorMessage = d.ErrorMessage!,
                ErrorDate = d.ProcessedDate ?? DateTime.UtcNow
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing errors for company: {Company}", companyName);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            await Task.Run(() => {
                // Check directory and file existence
                return Directory.Exists(_dataPath) && File.Exists(_documentsFile);
            });
            
            // Test read/write access
            var testFile = Path.Combine(_dataPath, "health_check.txt");
            await File.WriteAllTextAsync(testFile, "health_check");
            var content = await File.ReadAllTextAsync(testFile);
            File.Delete(testFile);
            
            return content == "health_check";
        }
        catch
        {
            return false;
        }
    }

    public string GetStorageType() => "Local File Storage";

    private async Task<List<DocumentInfo>> LoadDocumentsAsync()
    {
        try
        {
            if (!File.Exists(_documentsFile))
                return new List<DocumentInfo>();

            var json = await File.ReadAllTextAsync(_documentsFile);
            return JsonSerializer.Deserialize<List<DocumentInfo>>(json, _jsonOptions) ?? new List<DocumentInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load documents from file");
            return new List<DocumentInfo>();
        }
    }

    private async Task SaveDocumentsAsync(List<DocumentInfo> documents)
    {
        try
        {
            var json = JsonSerializer.Serialize(documents, _jsonOptions);
            await File.WriteAllTextAsync(_documentsFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save documents to file");
            throw;
        }
    }
}
