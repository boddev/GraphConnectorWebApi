using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpResources;

/// <summary>
/// MCP Resource representation for SEC Edgar documents
/// </summary>
public class McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("metadata")]
    public ResourceMetadata? Metadata { get; set; }
}

/// <summary>
/// Resource metadata containing SEC document-specific information
/// </summary>
public class ResourceMetadata
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("formType")]
    public string? FormType { get; set; }

    [JsonPropertyName("filingDate")]
    public DateTime? FilingDate { get; set; }

    [JsonPropertyName("cik")]
    public string? Cik { get; set; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("contentLength")]
    public long? ContentLength { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("edgarUrl")]
    public string? EdgarUrl { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }
}

/// <summary>
/// Resource content response with various content types
/// </summary>
public class ResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("metadata")]
    public ResourceMetadata? Metadata { get; set; }
}

/// <summary>
/// Parameters for resource listing and filtering
/// </summary>
public class ResourceListParameters
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("formType")]
    public string? FormType { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("limit")]
    [Range(1, 1000)]
    public int Limit { get; set; } = 100;

    [JsonPropertyName("offset")]
    [Range(0, int.MaxValue)]
    public int Offset { get; set; } = 0;

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "filingDate";

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Resource listing response
/// </summary>
public class ResourceListResponse
{
    [JsonPropertyName("resources")]
    public List<McpResource> Resources { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

/// <summary>
/// URI scheme for SEC Edgar document resources
/// </summary>
public static class ResourceUriScheme
{
    public const string Scheme = "sec-edgar";
    public const string Authority = "documents";
    
    /// <summary>
    /// Create a resource URI for a SEC document
    /// </summary>
    /// <param name="cik">Company CIK</param>
    /// <param name="formType">Form type (e.g., 10-K, 10-Q)</param>
    /// <param name="filingDate">Filing date</param>
    /// <param name="documentId">Unique document identifier</param>
    /// <returns>Resource URI</returns>
    public static string CreateDocumentUri(string cik, string formType, DateTime filingDate, string documentId)
    {
        var datePart = filingDate.ToString("yyyy-MM-dd");
        return $"{Scheme}://{Authority}/{cik}/{formType}/{datePart}/{documentId}";
    }

    /// <summary>
    /// Parse a resource URI to extract components
    /// </summary>
    /// <param name="uri">Resource URI</param>
    /// <returns>Parsed URI components or null if invalid</returns>
    public static ResourceUriComponents? ParseResourceUri(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        try
        {
            var parsedUri = new Uri(uri);
            
            if (parsedUri.Scheme != Scheme || parsedUri.Host != Authority)
                return null;

            var pathParts = parsedUri.AbsolutePath.Trim('/').Split('/');
            
            if (pathParts.Length < 4)
                return null;

            if (!DateTime.TryParse(pathParts[2], out var filingDate))
                return null;

            return new ResourceUriComponents
            {
                Cik = pathParts[0],
                FormType = pathParts[1],
                FilingDate = filingDate,
                DocumentId = pathParts.Length > 3 ? pathParts[3] : string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate if a URI follows the resource URI scheme
    /// </summary>
    public static bool IsValidResourceUri(string uri)
    {
        return ParseResourceUri(uri) != null;
    }
}

/// <summary>
/// Components of a parsed resource URI
/// </summary>
public class ResourceUriComponents
{
    public string Cik { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public DateTime FilingDate { get; set; }
    public string DocumentId { get; set; } = string.Empty;
}

/// <summary>
/// Standard MCP resource response wrapper
/// </summary>
public class McpResourceResponse<T>
{
    [JsonPropertyName("content")]
    public T Content { get; set; } = default(T)!;

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    public static McpResourceResponse<T> Success(T content, Dictionary<string, object>? metadata = null)
    {
        return new McpResourceResponse<T>
        {
            Content = content,
            IsError = false,
            Metadata = metadata
        };
    }

    public static McpResourceResponse<T> Error(string errorMessage)
    {
        return new McpResourceResponse<T>
        {
            IsError = true,
            ErrorMessage = errorMessage
        };
    }
}