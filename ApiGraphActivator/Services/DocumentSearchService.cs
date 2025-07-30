using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Text.Json;
using ApiGraphActivator.McpTools;

namespace ApiGraphActivator.Services;

/// <summary>
/// Service for document content search and retrieval operations
/// </summary>
public class DocumentSearchService
{
    private readonly ILogger<DocumentSearchService> _logger;
    private readonly ICrawlStorageService _storageService;
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string? _processedBlobContainerName;

    public DocumentSearchService(ILogger<DocumentSearchService> logger, ICrawlStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
        
        // Initialize blob client for content retrieval
        var connectionString = Environment.GetEnvironmentVariable("TableStorage");
        _processedBlobContainerName = Environment.GetEnvironmentVariable("BlobContainerName");
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                _blobServiceClient = new BlobServiceClient(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to initialize blob service client: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Search documents by company name with pagination
    /// </summary>
    public async Task<PaginatedResult<DocumentSearchResult>> SearchByCompanyAsync(CompanySearchParameters parameters)
    {
        try
        {
            var totalCount = await _storageService.GetSearchResultCountAsync(
                companyName: parameters.CompanyName,
                formTypes: parameters.FormTypes,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate);

            var documents = await _storageService.SearchByCompanyAsync(
                companyName: parameters.CompanyName,
                formTypes: parameters.FormTypes,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate,
                skip: parameters.Skip,
                take: parameters.PageSize);

            var searchResults = new List<DocumentSearchResult>();
            foreach (var doc in documents)
            {
                var result = await ConvertToSearchResult(doc, parameters.IncludeContent);
                searchResults.Add(result);
            }

            return new PaginatedResult<DocumentSearchResult>
            {
                Items = searchResults,
                TotalCount = totalCount,
                Page = parameters.Page,
                PageSize = parameters.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents by company: {CompanyName}", parameters.CompanyName);
            throw;
        }
    }

    /// <summary>
    /// Search documents by form type and date range with pagination
    /// </summary>
    public async Task<PaginatedResult<DocumentSearchResult>> SearchByFormTypeAsync(FormFilterParameters parameters)
    {
        try
        {
            var totalCount = await _storageService.GetSearchResultCountAsync(
                companyName: null,
                formTypes: parameters.FormTypes,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate);

            var documents = await _storageService.SearchByFormTypeAsync(
                formTypes: parameters.FormTypes ?? new List<string>(),
                companyNames: parameters.CompanyNames,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate,
                skip: parameters.Skip,
                take: parameters.PageSize);

            var searchResults = new List<DocumentSearchResult>();
            foreach (var doc in documents)
            {
                var result = await ConvertToSearchResult(doc, parameters.IncludeContent);
                searchResults.Add(result);
            }

            return new PaginatedResult<DocumentSearchResult>
            {
                Items = searchResults,
                TotalCount = totalCount,
                Page = parameters.Page,
                PageSize = parameters.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents by form type");
            throw;
        }
    }

    /// <summary>
    /// Search documents by content with full-text search capabilities
    /// </summary>
    public async Task<PaginatedResult<DocumentSearchResult>> SearchByContentAsync(ContentSearchParameters parameters)
    {
        try
        {
            // First get all matching documents based on metadata filters
            var totalCount = await _storageService.GetSearchResultCountAsync(
                companyName: null,
                formTypes: parameters.FormTypes,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate);

            // Get a larger set for content filtering (we'll filter by content and then paginate)
            var allDocuments = await _storageService.SearchByFormTypeAsync(
                formTypes: parameters.FormTypes ?? FormTypes.AllFormTypes,
                companyNames: parameters.CompanyNames,
                startDate: parameters.StartDate,
                endDate: parameters.EndDate,
                skip: 0,
                take: Math.Min(totalCount, 1000)); // Limit to avoid memory issues

            var searchResults = new List<DocumentSearchResult>();
            
            foreach (var doc in allDocuments)
            {
                var content = await GetDocumentContentAsync(doc.Id);
                if (string.IsNullOrEmpty(content))
                    continue;

                // Check if content matches search criteria
                var matchResult = CheckContentMatch(content, parameters.SearchText, parameters.ExactMatch, parameters.CaseSensitive);
                if (matchResult.IsMatch)
                {
                    var result = await ConvertToSearchResult(doc, true); // Always include content for content search
                    result.FullContent = content;
                    result.RelevanceScore = matchResult.RelevanceScore;
                    result.Highlights = matchResult.Highlights;
                    result.ContentPreview = GenerateContentPreview(content, parameters.SearchText, 300);
                    
                    searchResults.Add(result);
                }
            }

            // Sort by relevance score and apply pagination
            var sortedResults = searchResults
                .OrderByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => r.FilingDate)
                .ToList();

            var paginatedResults = sortedResults
                .Skip(parameters.Skip)
                .Take(parameters.PageSize)
                .ToList();

            return new PaginatedResult<DocumentSearchResult>
            {
                Items = paginatedResults,
                TotalCount = searchResults.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents by content: {SearchText}", parameters.SearchText);
            throw;
        }
    }

    /// <summary>
    /// Convert DocumentInfo to DocumentSearchResult
    /// </summary>
    private async Task<DocumentSearchResult> ConvertToSearchResult(DocumentInfo doc, bool includeContent)
    {
        var result = new DocumentSearchResult
        {
            Id = doc.Id,
            Title = $"{doc.CompanyName} {doc.Form} {doc.FilingDate:yyyy-MM-dd}",
            CompanyName = doc.CompanyName,
            FormType = doc.Form,
            FilingDate = doc.FilingDate,
            Url = doc.Url,
            RelevanceScore = 1.0 // Default relevance
        };

        if (includeContent)
        {
            var content = await GetDocumentContentAsync(doc.Id);
            result.ContentPreview = GenerateContentPreview(content, null, 300);
            result.FullContent = content;
        }

        return result;
    }

    /// <summary>
    /// Retrieve document content from blob storage
    /// </summary>
    private async Task<string?> GetDocumentContentAsync(string documentId)
    {
        if (_blobServiceClient == null || string.IsNullOrEmpty(_processedBlobContainerName))
        {
            return null;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_processedBlobContainerName);
            var blobClient = containerClient.GetBlobClient($"/openai/{documentId}.txt");
            
            var response = await blobClient.DownloadContentAsync();
            return response.Value.Content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to retrieve content for document {DocumentId}: {Message}", documentId, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Check if content matches search criteria and calculate relevance
    /// </summary>
    private (bool IsMatch, double RelevanceScore, List<string> Highlights) CheckContentMatch(
        string content, string searchText, bool exactMatch, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(searchText))
            return (false, 0, new List<string>());

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var highlights = new List<string>();
        double relevanceScore = 0;

        if (exactMatch)
        {
            var isMatch = content.Contains(searchText, comparison);
            if (isMatch)
            {
                relevanceScore = 1.0;
                highlights.Add(searchText);
            }
            return (isMatch, relevanceScore, highlights);
        }
        else
        {
            // Split search text into words for fuzzy matching
            var searchWords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matchedWords = 0;

            foreach (var word in searchWords)
            {
                if (content.Contains(word, comparison))
                {
                    matchedWords++;
                    highlights.Add(word);
                    relevanceScore += 1.0 / searchWords.Length;
                }
            }

            return (matchedWords > 0, relevanceScore, highlights);
        }
    }

    /// <summary>
    /// Generate a content preview with highlighted search terms
    /// </summary>
    private string GenerateContentPreview(string? content, string? searchText, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        // Clean up content
        var cleanContent = content.Trim();
        
        if (cleanContent.Length <= maxLength)
            return cleanContent;

        // If we have search text, try to find it and center the preview around it
        if (!string.IsNullOrEmpty(searchText))
        {
            var index = cleanContent.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var start = Math.Max(0, index - maxLength / 3);
                var length = Math.Min(maxLength, cleanContent.Length - start);
                return cleanContent.Substring(start, length) + (start + length < cleanContent.Length ? "..." : "");
            }
        }

        // Default: take from beginning
        return cleanContent.Substring(0, maxLength) + "...";
    }
}