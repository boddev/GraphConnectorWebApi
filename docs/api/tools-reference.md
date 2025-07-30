# Tool Reference

Complete reference for all MCP tools provided by the SEC Edgar server.

## Overview

The SEC Edgar MCP server provides three primary tools for accessing SEC filing documents:

1. **Company Search Tool** (`search_documents_by_company`)
2. **Form Filter Tool** (`filter_documents_by_form_and_date`)  
3. **Content Search Tool** (`search_document_content`)

All tools follow the MCP specification and return structured, paginated results.

## Common Parameters

### Pagination Parameters

All tools support pagination with these parameters:

| Parameter | Type | Required | Default | Max | Description |
|-----------|------|----------|---------|-----|-------------|
| `page` | integer | No | 1 | - | Page number (1-based) |
| `pageSize` | integer | No | 50 | 1000* | Number of results per page |

*Content search has a maximum page size of 100 for performance reasons.

### Date Parameters

Date parameters use ISO 8601 format (YYYY-MM-DD):

| Parameter | Type | Format | Example | Description |
|-----------|------|--------|---------|-------------|
| `startDate` | string | YYYY-MM-DD | "2023-01-01" | Start date for filing date range |
| `endDate` | string | YYYY-MM-DD | "2023-12-31" | End date for filing date range |

### Form Type Parameters

All tools accept these SEC form types:

| Form Type | Description | Usage |
|-----------|-------------|-------|
| `10-K` | Annual reports | Comprehensive yearly financial data |
| `10-Q` | Quarterly reports | Interim quarterly financial statements |
| `8-K` | Current reports | Significant company events |
| `10-K/A` | Amended annual reports | Corrections to annual reports |
| `10-Q/A` | Amended quarterly reports | Corrections to quarterly reports |
| `8-K/A` | Amended current reports | Corrections to current reports |

## Tool 1: Company Search

**Tool Name**: `search_documents_by_company`  
**Endpoint**: `POST /mcp/tools/company-search`

Search SEC filing documents by company name with optional filtering.

### Input Parameters

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `companyName` | string | Yes | Name of the company to search for | "Apple Inc." |
| `formTypes` | array[string] | No | Form types to filter by | ["10-K", "10-Q"] |
| `startDate` | string | No | Start date for filing range | "2023-01-01" |
| `endDate` | string | No | End date for filing range | "2023-12-31" |
| `includeContent` | boolean | No | Include document content | false |
| `page` | integer | No | Page number | 1 |
| `pageSize` | integer | No | Results per page (max 1000) | 50 |

### Example Request

```json
{
  "companyName": "Apple Inc.",
  "formTypes": ["10-K", "10-Q"],
  "startDate": "2023-01-01",
  "endDate": "2023-12-31",
  "includeContent": false,
  "page": 1,
  "pageSize": 10
}
```

### Example Response

```json
{
  "content": {
    "items": [
      {
        "id": "apple-10k-2024-01-26",
        "title": "Apple Inc. 10-K 2024-01-26",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "filingDate": "2024-01-26T00:00:00Z",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019324000007/aapl-20240126.htm",
        "contentPreview": "UNITED STATES SECURITIES AND EXCHANGE COMMISSION Washington, D.C. 20549 FORM 10-K...",
        "fullContent": null,
        "relevanceScore": 1.0,
        "highlights": null
      }
    ],
    "totalCount": 15,
    "page": 1,
    "pageSize": 10,
    "totalPages": 2,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "searchType": "company",
    "searchTerm": "Apple Inc.",
    "executionTime": "2024-01-15T10:30:00Z",
    "formTypes": ["10-K", "10-Q"],
    "startDate": "2023-01-01",
    "endDate": "2023-12-31"
  }
}
```

### Use Cases

- **Portfolio Analysis**: Get all filings for companies in a portfolio
- **Compliance Monitoring**: Track specific companies' disclosure history
- **Competitive Research**: Analyze competitor filing patterns
- **Due Diligence**: Review comprehensive filing history before investments

## Tool 2: Form Filter

**Tool Name**: `filter_documents_by_form_and_date`  
**Endpoint**: `POST /mcp/tools/form-filter`

Filter SEC filing documents by form type and date range with optional company filtering.

### Input Parameters

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `formTypes` | array[string] | No | Form types to filter by | ["10-K"] |
| `companyNames` | array[string] | No | Company names to filter by | ["Apple Inc.", "Microsoft Corporation"] |
| `startDate` | string | No | Start date for filing range | "2023-01-01" |
| `endDate` | string | No | End date for filing range | "2023-12-31" |
| `includeContent` | boolean | No | Include document content | false |
| `page` | integer | No | Page number | 1 |
| `pageSize` | integer | No | Results per page (max 1000) | 50 |

### Example Request

```json
{
  "formTypes": ["8-K"],
  "companyNames": ["Apple Inc.", "Microsoft Corporation"],
  "startDate": "2024-01-01",
  "includeContent": false,
  "page": 1,
  "pageSize": 20
}
```

### Example Response

```json
{
  "content": {
    "items": [
      {
        "id": "apple-8k-2024-02-15",
        "title": "Apple Inc. 8-K 2024-02-15",
        "companyName": "Apple Inc.",
        "formType": "8-K",
        "filingDate": "2024-02-15T00:00:00Z",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019324000008/aapl-20240215.htm",
        "contentPreview": "CURRENT REPORT Pursuant to Section 13 or 15(d) of the Securities Exchange Act...",
        "relevanceScore": 1.0
      },
      {
        "id": "microsoft-8k-2024-02-10",
        "title": "Microsoft Corporation 8-K 2024-02-10",
        "companyName": "Microsoft Corporation",
        "formType": "8-K",
        "filingDate": "2024-02-10T00:00:00Z",
        "url": "https://www.sec.gov/Archives/edgar/data/789019/000078901924000010/msft-20240210.htm",
        "contentPreview": "CURRENT REPORT Pursuant to Section 13 or 15(d) of the Securities Exchange Act...",
        "relevanceScore": 1.0
      }
    ],
    "totalCount": 45,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "searchType": "form",
    "executionTime": "2024-01-15T10:35:00Z",
    "formTypes": ["8-K"],
    "companyNames": ["Apple Inc.", "Microsoft Corporation"],
    "startDate": "2024-01-01"
  }
}
```

### Use Cases

- **Market Events Monitoring**: Track 8-K filings for material events
- **Earnings Analysis**: Monitor 10-Q filings across quarters
- **Annual Report Analysis**: Collect 10-K filings for comparative analysis
- **Industry Research**: Analyze filing patterns across multiple companies

## Tool 3: Content Search

**Tool Name**: `search_document_content`  
**Endpoint**: `POST /mcp/tools/content-search`

Perform full-text search within SEC filing document content with relevance scoring and highlighting.

### Input Parameters

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `searchText` | string | Yes | Text to search for | "artificial intelligence" |
| `companyNames` | array[string] | No | Company names to limit scope | ["Apple Inc."] |
| `formTypes` | array[string] | No | Form types to limit scope | ["10-K"] |
| `startDate` | string | No | Start date for filing range | "2023-01-01" |
| `endDate` | string | No | End date for filing range | "2023-12-31" |
| `exactMatch` | boolean | No | Search for exact phrase | false |
| `caseSensitive` | boolean | No | Case sensitive search | false |
| `page` | integer | No | Page number | 1 |
| `pageSize` | integer | No | Results per page (max 100) | 50 |

### Example Request

```json
{
  "searchText": "artificial intelligence",
  "companyNames": ["Apple Inc.", "Microsoft Corporation"],
  "formTypes": ["10-K"],
  "exactMatch": false,
  "caseSensitive": false,
  "startDate": "2023-01-01",
  "page": 1,
  "pageSize": 10
}
```

### Example Response

```json
{
  "content": {
    "items": [
      {
        "id": "apple-10k-2024-01-26",
        "title": "Apple Inc. 10-K 2024-01-26",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "filingDate": "2024-01-26T00:00:00Z",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019324000007/aapl-20240126.htm",
        "contentPreview": "...our investments in artificial intelligence and machine learning technologies continue to drive innovation across our product portfolio...",
        "relevanceScore": 0.87,
        "highlights": ["artificial intelligence", "AI", "machine learning"]
      },
      {
        "id": "microsoft-10k-2024-02-01",
        "title": "Microsoft Corporation 10-K 2024-02-01",
        "companyName": "Microsoft Corporation",
        "formType": "10-K",
        "filingDate": "2024-02-01T00:00:00Z",
        "url": "https://www.sec.gov/Archives/edgar/data/789019/000078901924000008/msft-20240201.htm",
        "contentPreview": "...Microsoft is at the forefront of artificial intelligence innovation, with significant investments in AI infrastructure and capabilities...",
        "relevanceScore": 0.92,
        "highlights": ["artificial intelligence", "AI innovation", "AI infrastructure"]
      }
    ],
    "totalCount": 8,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "searchType": "content",
    "searchTerm": "artificial intelligence",
    "executionTime": "2024-01-15T10:40:00Z",
    "companyNames": ["Apple Inc.", "Microsoft Corporation"],
    "formTypes": ["10-K"],
    "exactMatch": false,
    "caseSensitive": false,
    "startDate": "2023-01-01"
  }
}
```

### Content Search Features

- **Relevance Scoring**: Results sorted by relevance (0.0 to 1.0)
- **Highlighting**: Matched terms highlighted in content preview
- **Fuzzy Matching**: Finds related terms and variations
- **Phrase Search**: Use `exactMatch: true` for exact phrase matching
- **Case Sensitivity**: Control case sensitivity with `caseSensitive`

### Use Cases

- **ESG Research**: Search for sustainability, governance, and social impact terms
- **Technology Analysis**: Find mentions of specific technologies or innovations
- **Risk Assessment**: Search for risk-related keywords and phrases
- **Competitive Intelligence**: Analyze how companies discuss market conditions
- **Regulatory Compliance**: Find compliance-related disclosures

## Response Format

All tools return responses in this standardized format:

### Success Response

```json
{
  "content": {
    "items": [...],           // Array of search results
    "totalCount": 25,         // Total matching documents
    "page": 1,                // Current page number
    "pageSize": 10,           // Results per page
    "totalPages": 3,          // Total pages available
    "hasNextPage": true,      // Whether next page exists
    "hasPreviousPage": false  // Whether previous page exists
  },
  "isError": false,           // Error flag
  "errorMessage": null,       // Error message (null on success)
  "metadata": {               // Additional operation metadata
    "searchType": "company",
    "searchTerm": "Apple Inc.",
    "executionTime": "2024-01-15T10:30:00Z"
  }
}
```

### Error Response

```json
{
  "content": null,
  "isError": true,
  "errorMessage": "Invalid form types: INVALID-FORM. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A",
  "metadata": null
}
```

## Document Result Format

Each document in the `items` array has this structure:

```json
{
  "id": "unique-document-identifier",
  "title": "Document title with company, form type, and date",
  "companyName": "Official company name",
  "formType": "SEC form type (10-K, 10-Q, etc.)",
  "filingDate": "ISO 8601 date string",
  "url": "Direct link to SEC filing",
  "contentPreview": "First 300 characters of content (nullable)",
  "fullContent": "Complete document content (nullable, only if requested)",
  "relevanceScore": 0.85,  // Relevance score (0.0 to 1.0)
  "highlights": ["array", "of", "highlighted", "terms"]  // For content search
}
```

## Error Handling

Common error scenarios and messages:

### Validation Errors

- **Missing required fields**: "companyName is required"
- **Invalid form types**: "Invalid form types: X. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A"
- **Invalid date format**: "Invalid date format. Use YYYY-MM-DD"
- **Invalid date range**: "startDate must be before endDate"
- **Page size too large**: "pageSize cannot exceed 1000"

### System Errors

- **Storage connectivity**: "Storage backend unavailable"
- **SEC API issues**: "Unable to connect to SEC servers"
- **Rate limiting**: "Rate limit exceeded, please retry later"

## Performance Considerations

### Optimization Tips

1. **Use Date Ranges**: Limit searches with `startDate` and `endDate`
2. **Appropriate Page Sizes**: Use 50-100 for most scenarios
3. **Content Inclusion**: Set `includeContent: false` unless needed
4. **Specific Form Types**: Filter by specific form types when possible
5. **Company Filtering**: Use specific company names to reduce scope

### Rate Limits

- **SEC API**: Automatically handled with exponential backoff
- **Storage Operations**: Optimized with connection pooling
- **Concurrent Requests**: Server handles multiple simultaneous requests

### Caching

- **Tool Discovery**: Cached for performance
- **Search Results**: Consider client-side caching for repeated queries
- **Document Content**: Cached at storage layer

## Integration Examples

### Workflow: Monitor Company Events

```bash
# 1. Get recent 8-K filings for a company
curl -X POST /mcp/tools/company-search \
  -d '{"companyName": "Apple Inc.", "formTypes": ["8-K"], "startDate": "2024-01-01"}'

# 2. Search for specific event types
curl -X POST /mcp/tools/content-search \
  -d '{"searchText": "acquisition", "companyNames": ["Apple Inc."], "formTypes": ["8-K"]}'
```

### Workflow: Industry Analysis

```bash
# 1. Get all 10-K filings for tech companies
curl -X POST /mcp/tools/form-filter \
  -d '{"formTypes": ["10-K"], "companyNames": ["Apple Inc.", "Microsoft Corporation", "Alphabet Inc."]}'

# 2. Search for technology trends
curl -X POST /mcp/tools/content-search \
  -d '{"searchText": "cloud computing", "formTypes": ["10-K"], "companyNames": ["Apple Inc.", "Microsoft Corporation"]}'
```

This comprehensive tool reference provides everything needed to effectively use the SEC Edgar MCP server tools for document search and analysis.