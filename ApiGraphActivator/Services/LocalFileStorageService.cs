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

    public async Task MarkProcessedAsync(string url)
    {
        try
        {
            var documents = await LoadDocumentsAsync();
            var document = documents.FirstOrDefault(d => d.Url == url);
            
            if (document != null)
            {
                document.Processed = true;
                await SaveDocumentsAsync(documents);
                _logger.LogTrace("Marked document as processed: {Url}", url);
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

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            return Directory.Exists(_dataPath) && File.Exists(_documentsFile);
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
