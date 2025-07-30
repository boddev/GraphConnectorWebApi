using ApiGraphActivator.Services;
using ApiGraphActivator.McpTools;

namespace ApiGraphActivator.Tests.TestData;

/// <summary>
/// Builder for creating test data objects
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a sample DocumentInfo for testing
    /// </summary>
    public static DocumentInfo CreateDocumentInfo(
        string? companyName = null,
        string? form = null,
        DateTime? filingDate = null,
        string? url = null,
        string? id = null,
        bool processed = false,
        bool success = true)
    {
        var company = companyName ?? "Apple Inc.";
        var formType = form ?? "10-K";
        var filing = filingDate ?? DateTime.Now.AddDays(-30);
        var docUrl = url ?? $"https://www.sec.gov/Archives/edgar/data/320193/000032019323000077/{company.Replace(" ", "")}-{formType.Replace("/", "")}-{filing:yyyyMMdd}.htm";
        var documentId = id ?? DocumentIdGenerator.GenerateDocumentId(docUrl);

        return new DocumentInfo
        {
            Id = documentId,
            CompanyName = company,
            Form = formType,
            FilingDate = filing,
            Url = docUrl,
            Processed = processed,
            Success = success,
            ProcessedDate = processed ? DateTime.UtcNow : null
        };
    }

    /// <summary>
    /// Creates a collection of test documents
    /// </summary>
    public static List<DocumentInfo> CreateTestDocuments(int count = 10)
    {
        var companies = new[] { "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc.", "Alphabet Inc.", "Tesla Inc." };
        var forms = new[] { "10-K", "10-Q", "8-K" };
        var documents = new List<DocumentInfo>();

        var random = new Random(42); // Fixed seed for reproducible tests
        
        for (int i = 0; i < count; i++)
        {
            var company = companies[i % companies.Length];
            var form = forms[i % forms.Length];
            var filingDate = DateTime.Now.AddDays(-random.Next(30, 365));
            
            documents.Add(CreateDocumentInfo(company, form, filingDate));
        }

        return documents;
    }

    /// <summary>
    /// Creates a sample CompanySearchParameters for testing
    /// </summary>
    public static CompanySearchParameters CreateCompanySearchParameters(
        string? companyName = null,
        List<string>? formTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool includeContent = false,
        int page = 1,
        int pageSize = 50)
    {
        return new CompanySearchParameters
        {
            CompanyName = companyName ?? "Apple",
            FormTypes = formTypes,
            StartDate = startDate,
            EndDate = endDate,
            IncludeContent = includeContent,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Creates a sample FormFilterParameters for testing
    /// </summary>
    public static FormFilterParameters CreateFormFilterParameters(
        List<string>? formTypes = null,
        List<string>? companyNames = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool includeContent = false,
        int page = 1,
        int pageSize = 50)
    {
        return new FormFilterParameters
        {
            FormTypes = formTypes ?? new List<string> { "10-K" },
            CompanyNames = companyNames,
            StartDate = startDate,
            EndDate = endDate,
            IncludeContent = includeContent,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Creates a sample ContentSearchParameters for testing
    /// </summary>
    public static ContentSearchParameters CreateContentSearchParameters(
        string? searchText = null,
        List<string>? companyNames = null,
        List<string>? formTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool exactMatch = false,
        bool caseSensitive = false,
        int page = 1,
        int pageSize = 50)
    {
        return new ContentSearchParameters
        {
            SearchText = searchText ?? "revenue",
            CompanyNames = companyNames,
            FormTypes = formTypes,
            StartDate = startDate,
            EndDate = endDate,
            ExactMatch = exactMatch,
            CaseSensitive = caseSensitive,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Creates a sample DocumentSearchResult for testing
    /// </summary>
    public static DocumentSearchResult CreateDocumentSearchResult(
        string? id = null,
        string? title = null,
        string? companyName = null,
        string? formType = null,
        DateTime? filingDate = null,
        string? url = null,
        string? contentPreview = null,
        string? fullContent = null,
        double relevanceScore = 1.0,
        List<string>? highlights = null)
    {
        var company = companyName ?? "Apple Inc.";
        var form = formType ?? "10-K";
        var filing = filingDate ?? DateTime.Now.AddDays(-30);

        return new DocumentSearchResult
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Title = title ?? $"{company} {form} {filing:yyyy-MM-dd}",
            CompanyName = company,
            FormType = form,
            FilingDate = filing,
            Url = url ?? $"https://www.sec.gov/Archives/edgar/data/320193/000032019323000077/filing.htm",
            ContentPreview = contentPreview,
            FullContent = fullContent,
            RelevanceScore = relevanceScore,
            Highlights = highlights
        };
    }

    /// <summary>
    /// Creates a collection of test search results
    /// </summary>
    public static List<DocumentSearchResult> CreateTestSearchResults(int count = 5)
    {
        var results = new List<DocumentSearchResult>();
        var companies = new[] { "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc." };
        var forms = new[] { "10-K", "10-Q", "8-K" };

        for (int i = 0; i < count; i++)
        {
            var company = companies[i % companies.Length];
            var form = forms[i % forms.Length];
            var filingDate = DateTime.Now.AddDays(-i * 30);

            results.Add(CreateDocumentSearchResult(
                companyName: company,
                formType: form,
                filingDate: filingDate,
                relevanceScore: 1.0 - (i * 0.1)));
        }

        return results;
    }

    /// <summary>
    /// Creates a paginated result for testing
    /// </summary>
    public static PaginatedResult<T> CreatePaginatedResult<T>(
        List<T> items,
        int totalCount,
        int page = 1,
        int pageSize = 50)
    {
        return new PaginatedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}