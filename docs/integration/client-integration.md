# MCP Client Integration Guide

This guide shows how to integrate with the SEC Edgar MCP (Model Context Protocol) server to search and access SEC filing documents.

## Overview

The MCP server provides three primary tools for document search:
- **Company Search**: Find documents by company name
- **Form Filter**: Filter documents by form type and date
- **Content Search**: Full-text search within document content

## Quick Start

### 1. Server Setup

First, ensure the MCP server is running:

```bash
cd ApiGraphActivator
dotnet run
```

The server will be available at:
- HTTP: `http://localhost:5236`
- HTTPS: `https://localhost:7034`

### 2. Tool Discovery

Discover available tools:

```bash
curl -X GET http://localhost:5236/mcp/tools \
  -H "Content-Type: application/json"
```

Response:
```json
[
  {
    "name": "search_documents_by_company",
    "description": "Search SEC filing documents by company name",
    "endpoint": "/mcp/tools/company-search",
    "inputSchema": { ... }
  },
  {
    "name": "filter_documents_by_form_and_date", 
    "description": "Filter SEC filing documents by form type and date range",
    "endpoint": "/mcp/tools/form-filter",
    "inputSchema": { ... }
  },
  {
    "name": "search_document_content",
    "description": "Perform full-text search within SEC filing document content",
    "endpoint": "/mcp/tools/content-search", 
    "inputSchema": { ... }
  }
]
```

### 3. Basic Tool Usage

#### Company Search Example

```bash
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Apple Inc.",
    "formTypes": ["10-K", "10-Q"],
    "startDate": "2023-01-01",
    "endDate": "2024-12-31",
    "page": 1,
    "pageSize": 10
  }'
```

#### Content Search Example

```bash
curl -X POST http://localhost:5236/mcp/tools/content-search \
  -H "Content-Type: application/json" \
  -d '{
    "searchText": "artificial intelligence",
    "companyNames": ["Apple Inc."],
    "formTypes": ["10-K"],
    "exactMatch": false,
    "page": 1,
    "pageSize": 5
  }'
```

## Authentication

Currently, the MCP server uses the Microsoft Graph connector's authentication. Ensure the following are configured:

1. **Azure AD App Registration** with required permissions
2. **Client credentials** in configuration
3. **Admin consent** granted for the application

See the [Configuration Reference](../deployment/configuration-reference.md) for details.

## Error Handling

All MCP tools return standardized error responses:

```json
{
  "content": null,
  "isError": true,
  "errorMessage": "Invalid form types: INVALID-FORM. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A",
  "metadata": null
}
```

Common error scenarios:
- **Invalid form types**: Only supported SEC form types are accepted
- **Invalid date ranges**: Start date must be before end date
- **Pagination limits**: Page size cannot exceed maximum limits
- **Missing required fields**: Company name required for company search, search text for content search

## Rate Limiting and Performance

### Built-in Rate Limiting
- **SEC EDGAR requests**: Automatic rate limiting with exponential backoff
- **Storage operations**: Optimized queries with caching
- **Content search**: Limited to 100 results per page for performance

### Performance Tips
- Use pagination for large result sets
- Include `includeContent: false` unless full content is needed
- Use specific date ranges to narrow search scope
- Cache tool discovery results to avoid repeated calls

## Integration Patterns

### 1. Document Discovery Workflow

```bash
# 1. Discover companies
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{"companyName": "Apple", "pageSize": 5}'

# 2. Get specific form types
curl -X POST http://localhost:5236/mcp/tools/form-filter \
  -H "Content-Type: application/json" \
  -d '{"formTypes": ["10-K"], "companyNames": ["Apple Inc."], "pageSize": 10}'

# 3. Search within documents
curl -X POST http://localhost:5236/mcp/tools/content-search \
  -H "Content-Type: application/json" \
  -d '{"searchText": "revenue", "companyNames": ["Apple Inc."], "formTypes": ["10-K"]}'
```

### 2. Compliance Monitoring

```bash
# Monitor recent 8-K filings
curl -X POST http://localhost:5236/mcp/tools/form-filter \
  -H "Content-Type: application/json" \
  -d '{
    "formTypes": ["8-K"],
    "startDate": "2024-01-01",
    "companyNames": ["Apple Inc.", "Microsoft Corporation"],
    "pageSize": 50
  }'
```

### 3. Research and Analysis

```bash
# Find AI-related disclosures across tech companies
curl -X POST http://localhost:5236/mcp/tools/content-search \
  -H "Content-Type: application/json" \
  -d '{
    "searchText": "artificial intelligence",
    "companyNames": ["Apple Inc.", "Microsoft Corporation", "Alphabet Inc."],
    "formTypes": ["10-K", "10-Q"],
    "exactMatch": false,
    "pageSize": 20
  }'
```

## Storage Backend Configuration

The MCP server supports multiple storage backends. Configure via environment variables:

```bash
# In-Memory (Development)
export STORAGE_TYPE="InMemory"

# Local File Storage
export STORAGE_TYPE="LocalFile"
export STORAGE_PATH="/data/sec-filings"

# Azure Table Storage (Production)
export STORAGE_TYPE="AzureTable"
export TableStorage="DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."
```

## Monitoring and Diagnostics

### Health Check

```bash
curl -X GET http://localhost:5236/health
```

### Application Insights

If configured, telemetry is automatically sent to Application Insights:
- Request tracking for all tool executions
- Dependency tracking for storage operations
- Custom events for search operations
- Exception tracking for error analysis

### Logging

Enable detailed logging in configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ApiGraphActivator.McpTools": "Debug",
      "ApiGraphActivator.Services": "Debug"
    }
  }
}
```

## Next Steps

1. **Language-Specific Examples**: See [Python Examples](./python-examples.md) or [Node.js Examples](./nodejs-examples.md)
2. **Advanced Integration**: Review [Advanced Use Cases](../examples/advanced-examples.md)
3. **Production Deployment**: Follow the [MCP Server Setup](../deployment/mcp-server-setup.md) guide
4. **Troubleshooting**: Check the [Common Issues](../troubleshooting/common-issues.md) guide

## Sample Integration Checklist

- [ ] Server running and accessible
- [ ] Tool discovery working
- [ ] Basic company search functional
- [ ] Content search returning results
- [ ] Error handling implemented
- [ ] Pagination logic in place
- [ ] Authentication configured
- [ ] Storage backend selected
- [ ] Monitoring/logging configured
- [ ] Performance optimization applied