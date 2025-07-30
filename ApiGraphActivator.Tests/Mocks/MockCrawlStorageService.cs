using ApiGraphActivator.Services;

namespace ApiGraphActivator.Tests.Mocks;

/// <summary>
/// Mock implementation of ICrawlStorageService for testing
/// </summary>
public class MockCrawlStorageService : ICrawlStorageService
{
    private readonly List<DocumentInfo> _documents = new();
    private readonly List<ProcessingError> _errors = new();
    
    public Task InitializeAsync() => Task.CompletedTask;

    public Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url)
    {
        var document = new DocumentInfo
        {
            Id = DocumentIdGenerator.GenerateDocumentId(url),
            CompanyName = companyName,
            Form = form,
            FilingDate = filingDate,
            Url = url,
            Processed = false
        };
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task MarkProcessedAsync(string url, bool success = true, string? errorMessage = null)
    {
        var document = _documents.FirstOrDefault(d => d.Url == url);
        if (document != null)
        {
            document.Processed = true;
            document.Success = success;
            document.ErrorMessage = errorMessage;
            document.ProcessedDate = DateTime.UtcNow;
            
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                _errors.Add(new ProcessingError
                {
                    CompanyName = document.CompanyName,
                    Form = document.Form,
                    Url = url,
                    ErrorMessage = errorMessage,
                    ErrorDate = DateTime.UtcNow
                });
            }
        }
        return Task.CompletedTask;
    }

    public Task<List<DocumentInfo>> GetUnprocessedAsync()
    {
        return Task.FromResult(_documents.Where(d => !d.Processed).ToList());
    }

    public Task<CrawlMetrics> GetCrawlMetricsAsync(string? companyName = null)
    {
        var docs = string.IsNullOrEmpty(companyName) 
            ? _documents 
            : _documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase)).ToList();

        var metrics = new CrawlMetrics
        {
            CompanyName = companyName ?? "All Companies",
            TotalDocuments = docs.Count,
            ProcessedDocuments = docs.Count(d => d.Processed),
            SuccessfulDocuments = docs.Count(d => d.Processed && d.Success),
            FailedDocuments = docs.Count(d => d.Processed && !d.Success),
            LastProcessedDate = docs.Where(d => d.ProcessedDate.HasValue).Max(d => d.ProcessedDate),
            FormTypeCounts = docs.GroupBy(d => d.Form).ToDictionary(g => g.Key, g => g.Count())
        };

        return Task.FromResult(metrics);
    }

    public Task<List<ProcessingError>> GetProcessingErrorsAsync(string? companyName = null)
    {
        var errors = string.IsNullOrEmpty(companyName)
            ? _errors
            : _errors.Where(e => e.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult(errors);
    }

    public Task<Dictionary<int, YearlyMetrics>> GetYearlyMetricsAsync()
    {
        var yearlyMetrics = _documents
            .GroupBy(d => d.FilingDate.Year)
            .ToDictionary(g => g.Key, g => new YearlyMetrics
            {
                Year = g.Key,
                TotalDocuments = g.Count(),
                ProcessedDocuments = g.Count(d => d.Processed),
                SuccessfulDocuments = g.Count(d => d.Processed && d.Success),
                FailedDocuments = g.Count(d => d.Processed && !d.Success),
                FormTypeCounts = g.GroupBy(d => d.Form).ToDictionary(fg => fg.Key, fg => fg.Count()),
                Companies = g.Select(d => d.CompanyName).Distinct().ToList()
            });

        return Task.FromResult(yearlyMetrics);
    }

    public Task<Dictionary<int, YearlyMetrics>> GetCompanyYearlyMetricsAsync(string companyName)
    {
        var companyDocs = _documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
        
        var yearlyMetrics = companyDocs
            .GroupBy(d => d.FilingDate.Year)
            .ToDictionary(g => g.Key, g => new YearlyMetrics
            {
                Year = g.Key,
                TotalDocuments = g.Count(),
                ProcessedDocuments = g.Count(d => d.Processed),
                SuccessfulDocuments = g.Count(d => d.Processed && d.Success),
                FailedDocuments = g.Count(d => d.Processed && !d.Success),
                FormTypeCounts = g.GroupBy(d => d.Form).ToDictionary(fg => fg.Key, fg => fg.Count()),
                Companies = new List<string> { companyName }
            });

        return Task.FromResult(yearlyMetrics);
    }

    public Task<bool> IsHealthyAsync() => Task.FromResult(true);

    public string GetStorageType() => "Mock";

    public Task<List<DocumentInfo>> SearchByCompanyAsync(string companyName, List<string>? formTypes = null, 
        DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50)
    {
        var query = _documents.Where(d => 
            d.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));

        if (formTypes?.Any() == true)
        {
            query = query.Where(d => formTypes.Contains(d.Form));
        }

        if (startDate.HasValue)
        {
            query = query.Where(d => d.FilingDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(d => d.FilingDate <= endDate.Value);
        }

        var results = query
            .OrderByDescending(d => d.FilingDate)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<DocumentInfo>> SearchByFormTypeAsync(List<string> formTypes, List<string>? companyNames = null,
        DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50)
    {
        var query = _documents.Where(d => formTypes.Contains(d.Form));

        if (companyNames?.Any() == true)
        {
            query = query.Where(d => companyNames.Any(cn => 
                d.CompanyName.Contains(cn, StringComparison.OrdinalIgnoreCase)));
        }

        if (startDate.HasValue)
        {
            query = query.Where(d => d.FilingDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(d => d.FilingDate <= endDate.Value);
        }

        var results = query
            .OrderByDescending(d => d.FilingDate)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> GetSearchResultCountAsync(string? companyName = null, List<string>? formTypes = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _documents.AsQueryable();

        if (!string.IsNullOrEmpty(companyName))
        {
            query = query.Where(d => d.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));
        }

        if (formTypes?.Any() == true)
        {
            query = query.Where(d => formTypes.Contains(d.Form));
        }

        if (startDate.HasValue)
        {
            query = query.Where(d => d.FilingDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(d => d.FilingDate <= endDate.Value);
        }

        return Task.FromResult(query.Count());
    }

    // Helper methods for testing
    public void AddTestDocument(DocumentInfo document)
    {
        _documents.Add(document);
    }

    public void ClearTestData()
    {
        _documents.Clear();
        _errors.Clear();
    }

    public List<DocumentInfo> GetAllDocuments()
    {
        return _documents.ToList();
    }
}