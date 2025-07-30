# MCP Document Search and Retrieval Tools

This document describes the MCP (Model Context Protocol) document search and retrieval tools implemented for the SEC Edgar Graph Connector Web API.

## Overview

The MCP Document Tools provide comprehensive interfaces for both searching and retrieving SEC filing documents. The tools include advanced filtering, pagination, content search capabilities, and direct document retrieval by URL or filing ID.

## Available Tools

### Search Tools

### 1. Company Search Tool (`search_documents_by_company`)

**Endpoint**: `POST /mcp/tools/company-search`

Search SEC filing documents by company name with optional filtering.

**Parameters**:
- `companyName` (required): Name of the company to search for (supports partial matching)
- `formTypes` (optional): Array of form types to filter by (e.g., "10-K", "10-Q", "8-K")
- `startDate` (optional): Start date for filing date range (YYYY-MM-DD)
- `endDate` (optional): End date for filing date range (YYYY-MM-DD)
- `includeContent` (optional): Whether to include document content in results (default: false)
- `page` (optional): Page number for pagination (default: 1)
- `pageSize` (optional): Number of results per page (default: 50, max: 1000)

**Example**:
```json
{
  "companyName": "Apple",
  "formTypes": ["10-K", "10-Q"],
  "startDate": "2023-01-01",
  "endDate": "2024-12-31",
  "page": 1,
  "pageSize": 10
}
```

### 2. Form Filter Tool (`filter_documents_by_form_and_date`)

**Endpoint**: `POST /mcp/tools/form-filter`

Filter SEC filing documents by form type and date range with optional company filtering.

**Parameters**:
- `formTypes` (optional): Array of form types to filter by (defaults to all if not specified)
- `companyNames` (optional): Array of company names to filter by
- `startDate` (optional): Start date for filing date range (YYYY-MM-DD)
- `endDate` (optional): End date for filing date range (YYYY-MM-DD)
- `includeContent` (optional): Whether to include document content in results (default: false)
- `page` (optional): Page number for pagination (default: 1)
- `pageSize` (optional): Number of results per page (default: 50, max: 1000)

**Example**:
```json
{
  "formTypes": ["10-K"],
  "companyNames": ["Apple Inc.", "Microsoft Corporation"],
  "startDate": "2023-01-01",
  "page": 1,
  "pageSize": 20
}
```

### 3. Content Search Tool (`search_document_content`)

**Endpoint**: `POST /mcp/tools/content-search`

Perform full-text search within SEC filing document content with relevance scoring and highlighting.

**Parameters**:
- `searchText` (required): Text to search for within document content
- `companyNames` (optional): Array of company names to limit search scope
- `formTypes` (optional): Array of form types to limit search scope
- `startDate` (optional): Start date for filing date range (YYYY-MM-DD)
- `endDate` (optional): End date for filing date range (YYYY-MM-DD)
- `exactMatch` (optional): Whether to search for exact phrase match (default: false)
- `caseSensitive` (optional): Whether search should be case sensitive (default: false)
- `page` (optional): Page number for pagination (default: 1)
- `pageSize` (optional): Number of results per page (default: 50, max: 100 for content search)

**Example**:
```json
{
  "searchText": "artificial intelligence",
  "companyNames": ["Apple Inc."],
  "formTypes": ["10-K"],
  "exactMatch": false,
  "caseSensitive": false,
  "page": 1,
  "pageSize": 10
}
```

### Document Retrieval Tools

### 4. Document Retrieval by URL Tool (`retrieve_document_by_url`)

**Endpoint**: `POST /mcp/tools/retrieve-document`

Retrieve a specific SEC filing document by URL and extract its content and metadata.

**Parameters**:
- `url` (required): SEC document URL to retrieve (e.g., "https://www.sec.gov/Archives/edgar/...")
- `includeFullContent` (optional): Whether to include full document content in response (default: true)
- `maxContentLength` (optional): Maximum content length to return (default: 100000, max: 500000)

**Example**:
```json
{
  "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019324000010/aapl-20231230.htm",
  "includeFullContent": true,
  "maxContentLength": 150000
}
```

**Response Format**:
```json
{
  "content": {
    "documentId": "generated-hash-id",
    "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019324000010/aapl-20231230.htm",
    "contentType": "HTML",
    "companyName": "Apple Inc.",
    "formType": "10-K",
    "filingDate": "2023-12-30T00:00:00Z",
    "contentPreview": "First 300 characters...",
    "fullContent": "Complete document text content...",
    "contentLength": 125000,
    "retrievedAt": "2024-01-15T10:30:00Z"
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "retrievalType": "url",
    "contentType": "HTML",
    "originalContentLength": 125000,
    "includedFullContent": true,
    "executionTime": "2024-01-15T10:30:00.123Z"
  }
}
```

### 5. Document Retrieval by Filing ID Tool (`retrieve_document_by_id`)

**Endpoint**: `POST /mcp/tools/retrieve-document-by-id`

Retrieve a specific SEC filing document by filing ID. First searches stored documents, then retrieves content.

**Parameters**:
- `filingId` (required): Document filing ID or document identifier to retrieve
- `includeFullContent` (optional): Whether to include full document content in response (default: true)
- `maxContentLength` (optional): Maximum content length to return (default: 100000, max: 500000)

**Example**:
```json
{
  "filingId": "0000320193-24-000010",
  "includeFullContent": true,
  "maxContentLength": 100000
}
```

**Response Format**:
Same as Document Retrieval by URL tool, with additional metadata indicating whether the document was found in storage:

```json
{
  "metadata": {
    "retrievalType": "filingId",
    "originalFilingId": "0000320193-24-000010",
    "foundInStorage": true,
    "contentType": "HTML",
    "executionTime": "2024-01-15T10:30:00.123Z"
  }
}
```

## Supported Form Types

- `10-K`: Annual reports
- `10-Q`: Quarterly reports  
- `8-K`: Current reports
- `10-K/A`: Amended annual reports
- `10-Q/A`: Amended quarterly reports
- `8-K/A`: Amended current reports

## Response Format

All MCP tools return responses in the following format:

```json
{
  "content": {
    "items": [...],
    "totalCount": 25,
    "page": 1,
    "pageSize": 10,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "searchType": "company",
    "searchTerm": "Apple",
    "executionTime": "2025-07-30T22:16:51.1124252Z"
  }
}
```

### Document Search Result Format

Each document in the `items` array has the following structure:

```json
{
  "id": "unique-document-id",
  "title": "Company Name Form Type YYYY-MM-DD",
  "companyName": "Company Name",
  "formType": "10-K",
  "filingDate": "2024-03-15T00:00:00",
  "url": "https://www.sec.gov/Archives/edgar/data/...",
  "contentPreview": "First 300 characters of content...",
  "fullContent": "Complete document content (if requested)",
  "relevanceScore": 0.85,
  "highlights": ["matched", "terms"]
}
```

## Tool Discovery

**Endpoint**: `GET /mcp/tools`

Returns a list of all available MCP tools with their schemas and endpoints.

## Error Handling

All tools include comprehensive error handling with validation:

- **Parameter Validation**: Required fields, data types, value ranges
- **Form Type Validation**: Only supported form types are accepted
- **Date Validation**: Proper date format and logical date ranges
- **Pagination Validation**: Reasonable limits on page size

Error responses include detailed error messages:

```json
{
  "content": null,
  "isError": true,
  "errorMessage": "Invalid form types: INVALID-FORM. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A",
  "metadata": null
}
```

## Storage Backends

The search tools work with all supported storage backends:
- **In-Memory Storage**: For development and testing
- **Local File Storage**: For single-node deployments
- **Azure Table Storage**: For production cloud deployments

## Implementation Details

- **Search Performance**: Optimized queries with proper indexing and pagination
- **Content Retrieval**: Efficient blob storage access for full-text content
- **Relevance Scoring**: Fuzzy matching with relevance scoring for content search
- **Highlighting**: Search term highlighting in content preview
- **Caching**: Storage service caching for improved performance

## Integration

These MCP tools can be integrated with:
- AI agents and chatbots requiring SEC filing data
- Financial analysis applications requiring both document search and content extraction
- Compliance monitoring systems needing specific document retrieval
- Research platforms requiring structured document search and full document access
- Document processing workflows requiring SEC filing content analysis

The standardized MCP format ensures compatibility with various AI and automation frameworks that support the Model Context Protocol.

## Document Retrieval Features

The document retrieval tools provide:
- **Direct URL Access**: Retrieve any SEC document by its direct URL
- **Filing ID Lookup**: Find and retrieve documents using SEC accession numbers
- **Content Processing**: Extract and clean text from both PDF and HTML documents
- **Metadata Extraction**: Automatically extract company names, form types, and filing dates
- **Content Control**: Configurable content length limits and preview options
- **Error Handling**: Comprehensive validation and error reporting