using ApiGraphActivator.Services;
using ApiGraphActivator.McpTools;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ApiGraphActivator.McpResources;

/// <summary>
/// Service for managing MCP resources based on SEC Edgar documents
/// </summary>
public class ResourceService
{
    private readonly DocumentSearchService _documentSearchService;
    private readonly ILogger<ResourceService> _logger;
    
    public ResourceService(DocumentSearchService documentSearchService, ILogger<ResourceService> logger)
    {
        _documentSearchService = documentSearchService;
        _logger = logger;
    }

    /// <summary>
    /// List available resources with optional filtering
    /// </summary>
    public async Task<McpResourceResponse<ResourceListResponse>> ListResourcesAsync(ResourceListParameters parameters)
    {
        try
        {
            _logger.LogInformation("Listing resources with parameters: Company={Company}, FormType={FormType}, Limit={Limit}, Offset={Offset}", 
                parameters.CompanyName, parameters.FormType, parameters.Limit, parameters.Offset);

            // Convert resource parameters to document search parameters
            var searchParams = new FormFilterParameters
            {
                CompanyNames = string.IsNullOrEmpty(parameters.CompanyName) ? null : new List<string> { parameters.CompanyName },
                FormTypes = string.IsNullOrEmpty(parameters.FormType) ? FormTypes.AllFormTypes : new List<string> { parameters.FormType },
                StartDate = parameters.StartDate,
                EndDate = parameters.EndDate,
                Page = (parameters.Offset / parameters.Limit) + 1,
                PageSize = parameters.Limit,
                IncludeContent = false
            };

            _logger.LogInformation("Searching with FormTypes: {FormTypes}, CompanyNames: {CompanyNames}", 
                string.Join(",", searchParams.FormTypes ?? new List<string>()), 
                string.Join(",", searchParams.CompanyNames ?? new List<string>()));

            var searchResult = await _documentSearchService.SearchByFormTypeAsync(searchParams);

            var resources = new List<McpResource>();
            foreach (var doc in searchResult.Items)
            {
                _logger.LogInformation("Converting document: Id={Id}, Company={Company}, Form={Form}, URL={Url}", 
                    doc.Id, doc.CompanyName, doc.FormType, doc.Url);
                
                var resource = ConvertDocumentToResource(doc);
                if (resource != null)
                {
                    resources.Add(resource);
                    _logger.LogInformation("Successfully converted document to resource: {Uri}", resource.Uri);
                }
                else
                {
                    _logger.LogWarning("Failed to convert document to resource: {Id}", doc.Id);
                }
            }

            // Apply sorting if specified
            resources = ApplySorting(resources, parameters.SortBy, parameters.SortOrder);

            var response = new ResourceListResponse
            {
                Resources = resources,
                TotalCount = searchResult.TotalCount,
                Offset = parameters.Offset,
                Limit = parameters.Limit,
                HasMore = searchResult.HasNextPage
            };

            var metadata = new Dictionary<string, object>
            {
                ["searchType"] = "resourceList",
                ["executionTime"] = DateTime.UtcNow.ToString("O"),
                ["sortBy"] = parameters.SortBy,
                ["sortOrder"] = parameters.SortOrder
            };

            _logger.LogInformation("Resource listing completed: {ResourceCount} resources found", resources.Count);
            
            return McpResourceResponse<ResourceListResponse>.Success(response, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing resources");
            return McpResourceResponse<ResourceListResponse>.Error($"Failed to list resources: {ex.Message}");
        }
    }

    /// <summary>
    /// Get resource content by URI
    /// </summary>
    public async Task<McpResourceResponse<ResourceContent>> GetResourceContentAsync(string resourceUri)
    {
        try
        {
            _logger.LogInformation("Getting resource content for URI: {Uri}", resourceUri);

            // Parse the resource URI
            var uriComponents = ResourceUriScheme.ParseResourceUri(resourceUri);
            if (uriComponents == null)
            {
                return McpResourceResponse<ResourceContent>.Error($"Invalid resource URI: {resourceUri}");
            }

            // Find the document using form filter search (more reliable than content search)
            var searchParams = new FormFilterParameters
            {
                FormTypes = new List<string> { uriComponents.FormType },
                StartDate = uriComponents.FilingDate.Date,
                EndDate = uriComponents.FilingDate.Date.AddDays(1),
                PageSize = 100, // Get more results to find the specific document
                IncludeContent = true
            };

            var searchResult = await _documentSearchService.SearchByFormTypeAsync(searchParams);

            // Find the specific document by ID
            var document = searchResult.Items.FirstOrDefault(d => d.Id == uriComponents.DocumentId);
            
            if (document == null)
            {
                _logger.LogWarning("Document not found for URI: {Uri}, DocumentId: {DocumentId}", resourceUri, uriComponents.DocumentId);
                return McpResourceResponse<ResourceContent>.Error($"Resource not found: {resourceUri}");
            }

            // Create resource content
            var content = new ResourceContent
            {
                Uri = resourceUri,
                MimeType = "text/html",
                Content = document.FullContent ?? document.ContentPreview ?? "",
                Size = document.FullContent?.Length ?? document.ContentPreview?.Length ?? 0,
                Metadata = new ResourceMetadata
                {
                    CompanyName = document.CompanyName,
                    FormType = document.FormType,
                    FilingDate = document.FilingDate,
                    Cik = uriComponents.Cik,
                    ContentLength = document.FullContent?.Length ?? document.ContentPreview?.Length ?? 0,
                    LastModified = document.FilingDate,
                    EdgarUrl = document.Url,
                    DocumentType = "SEC Filing"
                }
            };

            var metadata = new Dictionary<string, object>
            {
                ["resourceType"] = "document",
                ["retrievedAt"] = DateTime.UtcNow.ToString("O"),
                ["sourceUri"] = document.Url
            };

            _logger.LogInformation("Resource content retrieved successfully for URI: {Uri}, Size: {Size} bytes", 
                resourceUri, content.Size);

            return McpResourceResponse<ResourceContent>.Success(content, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource content for URI: {Uri}", resourceUri);
            return McpResourceResponse<ResourceContent>.Error($"Failed to get resource content: {ex.Message}");
        }
    }

    /// <summary>
    /// Get resource metadata only (without content)
    /// </summary>
    public async Task<McpResourceResponse<McpResource>> GetResourceMetadataAsync(string resourceUri)
    {
        try
        {
            _logger.LogInformation("Getting resource metadata for URI: {Uri}", resourceUri);

            // Parse the resource URI
            var uriComponents = ResourceUriScheme.ParseResourceUri(resourceUri);
            if (uriComponents == null)
            {
                return McpResourceResponse<McpResource>.Error($"Invalid resource URI: {resourceUri}");
            }

            // Find the document using form filter search
            var searchParams = new FormFilterParameters
            {
                FormTypes = new List<string> { uriComponents.FormType },
                StartDate = uriComponents.FilingDate.Date,
                EndDate = uriComponents.FilingDate.Date.AddDays(1),
                PageSize = 100,
                IncludeContent = false
            };

            var searchResult = await _documentSearchService.SearchByFormTypeAsync(searchParams);

            // Find the specific document by ID
            var document = searchResult.Items.FirstOrDefault(d => d.Id == uriComponents.DocumentId);
            
            if (document == null)
            {
                _logger.LogWarning("Document not found for URI: {Uri}, DocumentId: {DocumentId}", resourceUri, uriComponents.DocumentId);
                return McpResourceResponse<McpResource>.Error($"Resource not found: {resourceUri}");
            }

            var resource = ConvertDocumentToResource(document);
            
            if (resource == null)
            {
                return McpResourceResponse<McpResource>.Error($"Failed to convert document to resource: {resourceUri}");
            }

            var metadata = new Dictionary<string, object>
            {
                ["resourceType"] = "document",
                ["retrievedAt"] = DateTime.UtcNow.ToString("O"),
                ["sourceUri"] = document.Url
            };

            _logger.LogInformation("Resource metadata retrieved successfully for URI: {Uri}", resourceUri);

            return McpResourceResponse<McpResource>.Success(resource, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource metadata for URI: {Uri}", resourceUri);
            return McpResourceResponse<McpResource>.Error($"Failed to get resource metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert a document search result to an MCP resource
    /// </summary>
    private McpResource? ConvertDocumentToResource(DocumentSearchResult document)
    {
        try
        {
            _logger.LogInformation("Converting document to resource: Id={Id}, Company={Company}, URL={Url}", 
                document.Id, document.CompanyName, document.Url);
            
            // Extract CIK from the document ID or URL
            var cik = ExtractCikFromDocument(document);
            
            _logger.LogInformation("Extracted CIK: {Cik} for document {Id}", cik, document.Id);

            // Create resource URI
            var resourceUri = ResourceUriScheme.CreateDocumentUri(cik, document.FormType, document.FilingDate, document.Id);

            var resource = new McpResource
            {
                Uri = resourceUri,
                Name = $"{document.CompanyName} - {document.FormType} ({document.FilingDate:yyyy-MM-dd})",
                Description = $"SEC {document.FormType} filing for {document.CompanyName} filed on {document.FilingDate:yyyy-MM-dd}",
                MimeType = "text/html",
                Metadata = new ResourceMetadata
                {
                    CompanyName = document.CompanyName,
                    FormType = document.FormType,
                    FilingDate = document.FilingDate,
                    Cik = cik,
                    ContentLength = document.FullContent?.Length ?? document.ContentPreview?.Length ?? 0,
                    LastModified = document.FilingDate,
                    EdgarUrl = document.Url,
                    DocumentType = "SEC Filing"
                }
            };
            
            _logger.LogInformation("Successfully created resource: {Uri}", resource.Uri);
            return resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting document to resource: {DocumentId}", document.Id);
            return null;
        }
    }

    /// <summary>
    /// Extract CIK from document data
    /// </summary>
    private string ExtractCikFromDocument(DocumentSearchResult document)
    {
        // Try to extract CIK from the URL first (more reliable)
        if (!string.IsNullOrEmpty(document.Url))
        {
            // Pattern: /data/1144879/ or /cik1144879/
            var cikMatch = Regex.Match(document.Url, @"/(?:data|cik)(\d+)");
            if (cikMatch.Success)
            {
                var cik = cikMatch.Groups[1].Value;
                // Pad to 10 digits as required by SEC
                return cik.PadLeft(10, '0');
            }
        }

        // Try to extract from document ID if it contains CIK pattern
        if (!string.IsNullOrEmpty(document.Id))
        {
            var cikMatch = Regex.Match(document.Id, @"(\d{7,10})");
            if (cikMatch.Success)
            {
                var cik = cikMatch.Groups[1].Value;
                return cik.PadLeft(10, '0');
            }
        }

        // Fallback: generate a consistent hash-based CIK from company name
        if (!string.IsNullOrEmpty(document.CompanyName))
        {
            var hash = document.CompanyName.GetHashCode();
            var cik = Math.Abs(hash).ToString();
            return cik.PadLeft(10, '0').Substring(0, 10);
        }

        // Last resort: use document ID hash
        if (!string.IsNullOrEmpty(document.Id))
        {
            var hash = document.Id.GetHashCode();
            var cik = Math.Abs(hash).ToString();
            return cik.PadLeft(10, '0').Substring(0, 10);
        }

        return "0000000000"; // Default CIK if all else fails
    }

    /// <summary>
    /// Apply sorting to resources list
    /// </summary>
    private List<McpResource> ApplySorting(List<McpResource> resources, string sortBy, string sortOrder)
    {
        var ascending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "filingdate" => ascending
                ? resources.OrderBy(r => r.Metadata?.FilingDate).ToList()
                : resources.OrderByDescending(r => r.Metadata?.FilingDate).ToList(),
            "companyname" => ascending
                ? resources.OrderBy(r => r.Metadata?.CompanyName).ToList()
                : resources.OrderByDescending(r => r.Metadata?.CompanyName).ToList(),
            "formtype" => ascending
                ? resources.OrderBy(r => r.Metadata?.FormType).ToList()
                : resources.OrderByDescending(r => r.Metadata?.FormType).ToList(),
            "name" => ascending
                ? resources.OrderBy(r => r.Name).ToList()
                : resources.OrderByDescending(r => r.Name).ToList(),
            _ => ascending
                ? resources.OrderBy(r => r.Metadata?.FilingDate).ToList()
                : resources.OrderByDescending(r => r.Metadata?.FilingDate).ToList()
        };
    }
}