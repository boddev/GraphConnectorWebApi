using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for retrieving specific documents by filing ID or URL
/// </summary>
public class DocumentRetrievalTool : McpToolBase
{
    private readonly ILogger<DocumentRetrievalTool> _logger;

    public DocumentRetrievalTool(ILogger<DocumentRetrievalTool> logger)
    {
        _logger = logger;
    }

    public override string Name => "retrieve_document_by_url";

    public override string Description => 
        "Retrieve a specific SEC filing document by URL. Extracts content and metadata from both PDF and HTML documents.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            url = new
            {
                type = "string",
                description = "SEC document URL to retrieve (e.g., https://www.sec.gov/Archives/edgar/...)"
            },
            includeFullContent = new
            {
                type = "boolean",
                description = "Whether to include full document content in response (default: true)"
            },
            maxContentLength = new
            {
                type = "integer",
                minimum = 1000,
                maximum = 500000,
                description = "Maximum content length to return (default: 100000 characters)"
            }
        },
        required = new[] { "url" }
    };

    public async Task<McpToolResponse<DocumentRetrievalResult>> ExecuteAsync(DocumentRetrievalParameters parameters)
    {
        try
        {
            _logger.LogInformation("Retrieving document from URL: {Url}", parameters.Url);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<DocumentRetrievalResult>.Error($"Validation failed: {errors}");
            }

            // Validate URL format
            if (!Uri.TryCreate(parameters.Url, UriKind.Absolute, out var uri) || 
                !parameters.Url.Contains("sec.gov", StringComparison.OrdinalIgnoreCase))
            {
                return McpToolResponse<DocumentRetrievalResult>.Error("Invalid SEC document URL format");
            }

            // Extract metadata from URL
            var metadata = ExtractMetadataFromUrl(parameters.Url);
            
            // Fetch document content
            string content;
            string contentType;
            
            if (parameters.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                content = await RetrievePdfContent(parameters.Url);
                contentType = "PDF";
            }
            else
            {
                content = await RetrieveHtmlContent(parameters.Url);
                contentType = "HTML";
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return McpToolResponse<DocumentRetrievalResult>.Error("No content could be extracted from the document");
            }

            // Apply content length limit
            var maxLength = parameters.MaxContentLength ?? 100000;
            var fullContent = content;
            var contentPreview = content.Length > 300 ? content.Substring(0, 300) + "..." : content;
            
            if (parameters.IncludeFullContent && content.Length > maxLength)
            {
                fullContent = content.Substring(0, maxLength) + $"\n\n[Content truncated - showing first {maxLength:N0} characters of {content.Length:N0} total]";
            }

            var result = new DocumentRetrievalResult
            {
                Url = parameters.Url,
                ContentType = contentType,
                ContentPreview = contentPreview,
                FullContent = parameters.IncludeFullContent ? fullContent : null,
                ContentLength = content.Length,
                CompanyName = metadata.CompanyName,
                FormType = metadata.FormType,
                FilingDate = metadata.FilingDate,
                DocumentId = GenerateDocumentId(parameters.Url),
                RetrievedAt = DateTime.UtcNow
            };

            var responseMetadata = new Dictionary<string, object>
            {
                ["retrievalType"] = "url",
                ["contentType"] = contentType,
                ["originalContentLength"] = content.Length,
                ["includedFullContent"] = parameters.IncludeFullContent,
                ["executionTime"] = DateTime.UtcNow.ToString("O")
            };

            _logger.LogInformation("Successfully retrieved document: {ContentLength} characters from {Url}", 
                content.Length, parameters.Url);

            return McpToolResponse<DocumentRetrievalResult>.Success(result, responseMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document from URL: {Url}", parameters.Url);
            return McpToolResponse<DocumentRetrievalResult>.Error($"Document retrieval failed: {ex.Message}");
        }
    }

    private async Task<string> RetrievePdfContent(string url)
    {
        try
        {
            _logger.LogInformation("Retrieving PDF content from: {Url}", url);
            
            // Use EdgarService's fetch method for consistency
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
            
            var pdfBytes = await httpClient.GetByteArrayAsync(url);
            
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException("Failed to fetch PDF content");
            }
            
            // Validate it's a real PDF
            if (!PdfProcessingService.IsValidPdf(pdfBytes))
            {
                throw new InvalidOperationException("Invalid PDF format");
            }
            
            // Extract text from PDF
            var content = await PdfProcessingService.ExtractTextFromPdfAsync(pdfBytes, 50); // Limit to 50 pages
            
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("No extractable text found in PDF");
            }
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF from URL: {Url}", url);
            throw;
        }
    }

    private async Task<string> RetrieveHtmlContent(string url)
    {
        try
        {
            _logger.LogInformation("Retrieving HTML content from: {Url}", url);
            
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
            
            var htmlContent = await httpClient.GetStringAsync(url);
            
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                throw new InvalidOperationException("No HTML content received");
            }

            // Process HTML content using the same logic as EdgarService
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);
            
            // Extract text content and clean it up
            var response = htmlDoc.DocumentNode.InnerText;
            
            // Decode HTML entities (like &amp;, &lt;, &gt;, etc.)
            response = System.Net.WebUtility.HtmlDecode(response);
            
            // Remove XML declarations and processing instructions
            response = Regex.Replace(response, @"<\?xml[^>]*\?>", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"<\?[^>]*\?>", "", RegexOptions.IgnoreCase);
            
            // Remove checkbox symbols and other special Unicode characters
            response = response.Replace("☐", ""); // Empty checkbox
            response = response.Replace("☑", ""); // Checked checkbox
            response = response.Replace("☒", ""); // X-marked checkbox
            response = response.Replace("✓", ""); // Checkmark
            response = response.Replace("✗", ""); // X mark
            
            // Remove XBRL namespace declarations and technical metadata
            response = Regex.Replace(response, @"iso4217:\w+", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"xbrli:\w+", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"\b\d{10}\b", ""); // Remove 10-digit numbers that look like IDs
            
            // Clean up whitespace and formatting
            response = Regex.Replace(response, @"\s+", " ", RegexOptions.Multiline);
            response = response.Trim();
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTML from URL: {Url}", url);
            throw;
        }
    }

    private DocumentMetadata ExtractMetadataFromUrl(string url)
    {
        var metadata = new DocumentMetadata();
        
        try
        {
            // Extract metadata from SEC URL patterns
            // Example: https://www.sec.gov/Archives/edgar/data/320193/000032019324000123/aapl-20240930.htm
            var uri = new Uri(url);
            var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // Try to extract CIK (company identifier) from path
            if (pathParts.Length >= 4 && pathParts[0] == "Archives" && pathParts[1] == "edgar" && pathParts[2] == "data")
            {
                if (pathParts.Length >= 4)
                {
                    // Extract potential form type and date from filename
                    var filename = Path.GetFileNameWithoutExtension(pathParts.Last());
                    
                    // Common patterns like "aapl-20240930" or "form10k-20240930"
                    var dateParts = Regex.Matches(filename, @"\d{8}"); // YYYYMMDD format
                    if (dateParts.Count > 0)
                    {
                        var dateStr = dateParts[0].Value;
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var filingDate))
                        {
                            metadata.FilingDate = filingDate;
                        }
                    }
                    
                    // Try to identify form type from filename or path
                    var formTypeMatch = Regex.Match(filename, @"(10-?[KQ]|8-?K)", RegexOptions.IgnoreCase);
                    if (formTypeMatch.Success)
                    {
                        metadata.FormType = formTypeMatch.Value.ToUpper();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract metadata from URL: {Url}", url);
        }
        
        return metadata;
    }

    private string GenerateDocumentId(string url)
    {
        // Use the existing DocumentIdGenerator for consistency
        return DocumentIdGenerator.GenerateDocumentId(url);
    }
}

/// <summary>
/// Parameters for document retrieval by URL
/// </summary>
public class DocumentRetrievalParameters
{
    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("includeFullContent")]
    public bool IncludeFullContent { get; set; } = true;

    [JsonPropertyName("maxContentLength")]
    [Range(1000, 500000)]
    public int? MaxContentLength { get; set; } = 100000;
}

/// <summary>
/// Result of document retrieval operation
/// </summary>
public class DocumentRetrievalResult
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("formType")]
    public string? FormType { get; set; }

    [JsonPropertyName("filingDate")]
    public DateTime? FilingDate { get; set; }

    [JsonPropertyName("contentPreview")]
    public string ContentPreview { get; set; } = string.Empty;

    [JsonPropertyName("fullContent")]
    public string? FullContent { get; set; }

    [JsonPropertyName("contentLength")]
    public int ContentLength { get; set; }

    [JsonPropertyName("retrievedAt")]
    public DateTime RetrievedAt { get; set; }
}

/// <summary>
/// MCP tool for retrieving documents by filing ID (searches stored documents first)
/// </summary>
public class DocumentByIdTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly DocumentRetrievalTool _retrievalTool;
    private readonly ILogger<DocumentByIdTool> _logger;

    public DocumentByIdTool(DocumentSearchService searchService, DocumentRetrievalTool retrievalTool, ILogger<DocumentByIdTool> logger)
    {
        _searchService = searchService;
        _retrievalTool = retrievalTool;
        _logger = logger;
    }

    public override string Name => "retrieve_document_by_id";

    public override string Description => 
        "Retrieve a specific SEC filing document by filing ID. First searches stored documents, then retrieves content and metadata.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            filingId = new
            {
                type = "string",
                description = "Document filing ID or document identifier to retrieve"
            },
            includeFullContent = new
            {
                type = "boolean",
                description = "Whether to include full document content in response (default: true)"
            },
            maxContentLength = new
            {
                type = "integer",
                minimum = 1000,
                maximum = 500000,
                description = "Maximum content length to return (default: 100000 characters)"
            }
        },
        required = new[] { "filingId" }
    };

    public async Task<McpToolResponse<DocumentRetrievalResult>> ExecuteAsync(DocumentByIdParameters parameters)
    {
        try
        {
            _logger.LogInformation("Retrieving document by ID: {FilingId}", parameters.FilingId);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<DocumentRetrievalResult>.Error($"Validation failed: {errors}");
            }

            // First, try to find the document in our stored data using search
            var searchParams = new ContentSearchParameters
            {
                SearchText = parameters.FilingId,
                PageSize = 10,
                Page = 1,
                ExactMatch = true
            };

            var searchResult = await _searchService.SearchByContentAsync(searchParams);
            
            string? documentUrl = null;
            DocumentSearchResult? foundDocument = null;

            // Look for exact ID match first
            foundDocument = searchResult.Items.FirstOrDefault(d => 
                string.Equals(d.Id, parameters.FilingId, StringComparison.OrdinalIgnoreCase));

            // If not found by ID, try searching by content or title
            if (foundDocument == null)
            {
                foundDocument = searchResult.Items.FirstOrDefault(d =>
                    d.Title.Contains(parameters.FilingId, StringComparison.OrdinalIgnoreCase) ||
                    d.Url.Contains(parameters.FilingId, StringComparison.OrdinalIgnoreCase));
            }

            if (foundDocument != null)
            {
                documentUrl = foundDocument.Url;
                _logger.LogInformation("Found document by ID in stored data: {Url}", documentUrl);
            }
            else
            {
                // If no document found in storage, try to construct URL from filing ID
                // This is a fallback for direct SEC filing IDs
                if (IsSecFilingId(parameters.FilingId))
                {
                    documentUrl = ConstructSecUrl(parameters.FilingId);
                    _logger.LogInformation("Constructed SEC URL from filing ID: {Url}", documentUrl);
                }
                else
                {
                    return McpToolResponse<DocumentRetrievalResult>.Error(
                        $"Document not found with filing ID: {parameters.FilingId}");
                }
            }

            // Now retrieve the document content using the URL
            var retrievalParams = new DocumentRetrievalParameters
            {
                Url = documentUrl,
                IncludeFullContent = parameters.IncludeFullContent,
                MaxContentLength = parameters.MaxContentLength
            };

            var retrievalResult = await _retrievalTool.ExecuteAsync(retrievalParams);
            
            if (retrievalResult.IsError)
            {
                return McpToolResponse<DocumentRetrievalResult>.Error(retrievalResult.ErrorMessage!);
            }

            // Enhance the result with information from stored document if available
            if (foundDocument != null)
            {
                var enhancedResult = retrievalResult.Content;
                enhancedResult.CompanyName = foundDocument.CompanyName;
                enhancedResult.FormType = foundDocument.FormType;
                enhancedResult.FilingDate = foundDocument.FilingDate;
                enhancedResult.DocumentId = foundDocument.Id;
            }

            var metadata = retrievalResult.Metadata ?? new Dictionary<string, object>();
            metadata["retrievalType"] = "filingId";
            metadata["originalFilingId"] = parameters.FilingId;
            metadata["foundInStorage"] = foundDocument != null;
            
            _logger.LogInformation("Successfully retrieved document by ID: {FilingId}", parameters.FilingId);

            return McpToolResponse<DocumentRetrievalResult>.Success(retrievalResult.Content, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document by ID: {FilingId}", parameters.FilingId);
            return McpToolResponse<DocumentRetrievalResult>.Error($"Document retrieval failed: {ex.Message}");
        }
    }

    private bool IsSecFilingId(string filingId)
    {
        // Check if it looks like a SEC filing ID (various patterns)
        // Examples: 0000320193-24-000123, 320193-24-000123, etc.
        return Regex.IsMatch(filingId, @"^\d{6,10}-\d{2}-\d{6}$") ||
               Regex.IsMatch(filingId, @"^0{4}\d{6}-\d{2}-\d{6}$");
    }

    private string ConstructSecUrl(string filingId)
    {
        // Basic SEC URL construction for accession numbers
        // This is a simplified approach - in practice you'd need more sophisticated mapping
        var parts = filingId.Split('-');
        if (parts.Length == 3)
        {
            var cik = parts[0].TrimStart('0');
            var year = "20" + parts[1];
            var sequence = parts[2];
            
            // Construct a basic SEC EDGAR URL
            return $"https://www.sec.gov/Archives/edgar/data/{cik}/{filingId.Replace("-", "")}/{filingId}.txt";
        }
        
        throw new ArgumentException($"Cannot construct SEC URL from filing ID: {filingId}");
    }
}

/// <summary>
/// Parameters for document retrieval by filing ID
/// </summary>
public class DocumentByIdParameters
{
    [Required]
    [JsonPropertyName("filingId")]
    public string FilingId { get; set; } = string.Empty;

    [JsonPropertyName("includeFullContent")]
    public bool IncludeFullContent { get; set; } = true;

    [JsonPropertyName("maxContentLength")]
    [Range(1000, 500000)]
    public int? MaxContentLength { get; set; } = 100000;
}

/// <summary>
/// Document metadata extracted from URL and content
/// </summary>
public class DocumentMetadata
{
    public string? CompanyName { get; set; }
    public string? FormType { get; set; }
    public DateTime? FilingDate { get; set; }
}