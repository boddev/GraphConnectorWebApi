using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class InMemoryStorageService : ICrawlStorageService
{
    private readonly ILogger<InMemoryStorageService> _logger;
    private readonly List<DocumentInfo> _documents = new();
    private readonly object _lock = new object();

    public InMemoryStorageService(ILogger<InMemoryStorageService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
        _logger.LogInformation("In-memory storage service initialized");
    }

    public async Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                // Check if document already exists
                if (_documents.Any(d => d.Url == url))
                {
                    _logger.LogTrace("Document already tracked: {Url}", url);
                    return;
                }

                var document = new DocumentInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    CompanyName = companyName,
                    Form = form,
                    FilingDate = filingDate,
                    Url = url,
                    Processed = false
                };

                _documents.Add(document);
                _logger.LogTrace("Tracked new document: {Company} - {Form} - {Url}", companyName, form, url);
            }
        });
    }

    public async Task MarkProcessedAsync(string url, bool success = true, string? errorMessage = null)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var document = _documents.FirstOrDefault(d => d.Url == url);
                if (document != null)
                {
                    document.Processed = true;
                    document.ProcessedDate = DateTime.UtcNow;
                    document.Success = success;
                    document.ErrorMessage = errorMessage;
                    _logger.LogTrace("Marked document as processed: {Url} - Success: {Success}", url, success);
                }
                else
                {
                    _logger.LogWarning("Document not found for processing: {Url}", url);
                }
            }
        });
    }

    public async Task<List<DocumentInfo>> GetUnprocessedAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _documents.Where(d => !d.Processed).ToList();
            }
        });
    }

    public async Task<CrawlMetrics> GetCrawlMetricsAsync(string? companyName = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var documents = _documents.AsEnumerable();
                
                if (!string.IsNullOrEmpty(companyName))
                {
                    documents = documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
                }

                var docList = documents.ToList();
                
                return new CrawlMetrics
                {
                    CompanyName = companyName ?? "All Companies",
                    TotalDocuments = docList.Count,
                    ProcessedDocuments = docList.Count(d => d.Processed),
                    SuccessfulDocuments = docList.Count(d => d.Processed && d.Success),
                    FailedDocuments = docList.Count(d => d.Processed && !d.Success),
                    LastProcessedDate = docList.Where(d => d.ProcessedDate.HasValue).Max(d => d.ProcessedDate),
                    FormTypeCounts = docList.GroupBy(d => d.Form).ToDictionary(g => g.Key, g => g.Count())
                };
            }
        });
    }

    public async Task<List<ProcessingError>> GetProcessingErrorsAsync(string? companyName = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var errorDocs = _documents.Where(d => d.Processed && !d.Success && !string.IsNullOrEmpty(d.ErrorMessage));
                
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
        });
    }

    public async Task<bool> IsHealthyAsync()
    {
        return await Task.FromResult(true);
    }

    public string GetStorageType() => "In-Memory Storage";
}
