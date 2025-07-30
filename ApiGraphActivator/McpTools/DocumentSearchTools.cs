using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for company-based document search
/// </summary>
public class CompanySearchTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly ILogger<CompanySearchTool> _logger;

    public CompanySearchTool(DocumentSearchService searchService, ILogger<CompanySearchTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public override string Name => "search_documents_by_company";

    public override string Description => 
        "Search SEC filing documents by company name. Supports filtering by form types, date ranges, and pagination.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            companyName = new
            {
                type = "string",
                description = "Name of the company to search for (supports partial matching)"
            },
            formTypes = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional list of form types to filter by (e.g., '10-K', '10-Q', '8-K')"
            },
            startDate = new
            {
                type = "string",
                format = "date",
                description = "Optional start date for filing date range (YYYY-MM-DD)"
            },
            endDate = new
            {
                type = "string",
                format = "date",
                description = "Optional end date for filing date range (YYYY-MM-DD)"
            },
            includeContent = new
            {
                type = "boolean",
                description = "Whether to include document content in results (default: false)"
            },
            page = new
            {
                type = "integer",
                minimum = 1,
                description = "Page number for pagination (default: 1)"
            },
            pageSize = new
            {
                type = "integer",
                minimum = 1,
                maximum = 1000,
                description = "Number of results per page (default: 50, max: 1000)"
            }
        },
        required = new[] { "companyName" }
    };

    public async Task<McpToolResponse<PaginatedResult<DocumentSearchResult>>> ExecuteAsync(CompanySearchParameters parameters)
    {
        try
        {
            _logger.LogInformation("Executing company search for: {CompanyName}", parameters.CompanyName);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Validation failed: {errors}");
            }

            // Validate form types if provided
            if (parameters.FormTypes?.Any() == true)
            {
                var invalidFormTypes = parameters.FormTypes.Where(ft => !FormTypes.IsValidFormType(ft)).ToList();
                if (invalidFormTypes.Any())
                {
                    return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error(
                        $"Invalid form types: {string.Join(", ", invalidFormTypes)}. Valid types: {string.Join(", ", FormTypes.AllFormTypes)}");
                }
            }

            var result = await _searchService.SearchByCompanyAsync(parameters);
            
            var metadata = new Dictionary<string, object>
            {
                ["searchType"] = "company",
                ["searchTerm"] = parameters.CompanyName,
                ["executionTime"] = DateTime.UtcNow.ToString("O")
            };

            if (parameters.FormTypes?.Any() == true)
                metadata["formTypes"] = parameters.FormTypes;
            
            if (parameters.StartDate.HasValue)
                metadata["startDate"] = parameters.StartDate.Value.ToString("yyyy-MM-dd");
            
            if (parameters.EndDate.HasValue)
                metadata["endDate"] = parameters.EndDate.Value.ToString("yyyy-MM-dd");

            _logger.LogInformation("Company search completed: {ResultCount} results for {CompanyName}", 
                result.Items.Count, parameters.CompanyName);

            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(result, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing company search for: {CompanyName}", parameters.CompanyName);
            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Search failed: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP tool for form type and date range filtering
/// </summary>
public class FormFilterTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly ILogger<FormFilterTool> _logger;

    public FormFilterTool(DocumentSearchService searchService, ILogger<FormFilterTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public override string Name => "filter_documents_by_form_and_date";

    public override string Description => 
        "Filter SEC filing documents by form type and date range. Supports multiple form types and company filtering.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            formTypes = new
            {
                type = "array",
                items = new { type = "string" },
                description = "List of form types to filter by (e.g., '10-K', '10-Q', '8-K')"
            },
            companyNames = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional list of company names to filter by"
            },
            startDate = new
            {
                type = "string",
                format = "date",
                description = "Optional start date for filing date range (YYYY-MM-DD)"
            },
            endDate = new
            {
                type = "string",
                format = "date",
                description = "Optional end date for filing date range (YYYY-MM-DD)"
            },
            includeContent = new
            {
                type = "boolean",
                description = "Whether to include document content in results (default: false)"
            },
            page = new
            {
                type = "integer",
                minimum = 1,
                description = "Page number for pagination (default: 1)"
            },
            pageSize = new
            {
                type = "integer",
                minimum = 1,
                maximum = 1000,
                description = "Number of results per page (default: 50, max: 1000)"
            }
        }
    };

    public async Task<McpToolResponse<PaginatedResult<DocumentSearchResult>>> ExecuteAsync(FormFilterParameters parameters)
    {
        try
        {
            _logger.LogInformation("Executing form filter search");
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Validation failed: {errors}");
            }

            // Default to all form types if none specified
            if (parameters.FormTypes?.Any() != true)
            {
                parameters.FormTypes = FormTypes.AllFormTypes;
            }

            // Validate form types
            var invalidFormTypes = parameters.FormTypes.Where(ft => !FormTypes.IsValidFormType(ft)).ToList();
            if (invalidFormTypes.Any())
            {
                return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error(
                    $"Invalid form types: {string.Join(", ", invalidFormTypes)}. Valid types: {string.Join(", ", FormTypes.AllFormTypes)}");
            }

            var result = await _searchService.SearchByFormTypeAsync(parameters);
            
            var metadata = new Dictionary<string, object>
            {
                ["searchType"] = "formFilter",
                ["formTypes"] = parameters.FormTypes,
                ["executionTime"] = DateTime.UtcNow.ToString("O")
            };

            if (parameters.CompanyNames?.Any() == true)
                metadata["companyNames"] = parameters.CompanyNames;
            
            if (parameters.StartDate.HasValue)
                metadata["startDate"] = parameters.StartDate.Value.ToString("yyyy-MM-dd");
            
            if (parameters.EndDate.HasValue)
                metadata["endDate"] = parameters.EndDate.Value.ToString("yyyy-MM-dd");

            _logger.LogInformation("Form filter search completed: {ResultCount} results", result.Items.Count);

            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(result, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing form filter search");
            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Search failed: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP tool for full-text content search
/// </summary>
public class ContentSearchTool : McpToolBase
{
    private readonly DocumentSearchService _searchService;
    private readonly ILogger<ContentSearchTool> _logger;

    public ContentSearchTool(DocumentSearchService searchService, ILogger<ContentSearchTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public override string Name => "search_document_content";

    public override string Description => 
        "Perform full-text search within SEC filing document content. Supports exact matching, case sensitivity, and filtering by company/form type.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            searchText = new
            {
                type = "string",
                description = "Text to search for within document content"
            },
            companyNames = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional list of company names to limit search scope"
            },
            formTypes = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional list of form types to limit search scope (e.g., '10-K', '10-Q', '8-K')"
            },
            startDate = new
            {
                type = "string",
                format = "date",
                description = "Optional start date for filing date range (YYYY-MM-DD)"
            },
            endDate = new
            {
                type = "string",
                format = "date",
                description = "Optional end date for filing date range (YYYY-MM-DD)"
            },
            exactMatch = new
            {
                type = "boolean",
                description = "Whether to search for exact phrase match (default: false)"
            },
            caseSensitive = new
            {
                type = "boolean",
                description = "Whether search should be case sensitive (default: false)"
            },
            page = new
            {
                type = "integer",
                minimum = 1,
                description = "Page number for pagination (default: 1)"
            },
            pageSize = new
            {
                type = "integer",
                minimum = 1,
                maximum = 100,
                description = "Number of results per page (default: 50, max: 100 for content search)"
            }
        },
        required = new[] { "searchText" }
    };

    public async Task<McpToolResponse<PaginatedResult<DocumentSearchResult>>> ExecuteAsync(ContentSearchParameters parameters)
    {
        try
        {
            _logger.LogInformation("Executing content search for: {SearchText}", parameters.SearchText);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Validation failed: {errors}");
            }

            // Validate form types if provided
            if (parameters.FormTypes?.Any() == true)
            {
                var invalidFormTypes = parameters.FormTypes.Where(ft => !FormTypes.IsValidFormType(ft)).ToList();
                if (invalidFormTypes.Any())
                {
                    return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error(
                        $"Invalid form types: {string.Join(", ", invalidFormTypes)}. Valid types: {string.Join(", ", FormTypes.AllFormTypes)}");
                }
            }

            // Limit page size for content search as it's more resource intensive
            parameters.PageSize = Math.Min(parameters.PageSize, 100);

            var result = await _searchService.SearchByContentAsync(parameters);
            
            var metadata = new Dictionary<string, object>
            {
                ["searchType"] = "content",
                ["searchTerm"] = parameters.SearchText,
                ["exactMatch"] = parameters.ExactMatch,
                ["caseSensitive"] = parameters.CaseSensitive,
                ["executionTime"] = DateTime.UtcNow.ToString("O")
            };

            if (parameters.FormTypes?.Any() == true)
                metadata["formTypes"] = parameters.FormTypes;
            
            if (parameters.CompanyNames?.Any() == true)
                metadata["companyNames"] = parameters.CompanyNames;
            
            if (parameters.StartDate.HasValue)
                metadata["startDate"] = parameters.StartDate.Value.ToString("yyyy-MM-dd");
            
            if (parameters.EndDate.HasValue)
                metadata["endDate"] = parameters.EndDate.Value.ToString("yyyy-MM-dd");

            _logger.LogInformation("Content search completed: {ResultCount} results for '{SearchText}'", 
                result.Items.Count, parameters.SearchText);

            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(result, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing content search for: {SearchText}", parameters.SearchText);
            return McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error($"Search failed: {ex.Message}");
        }
    }
}