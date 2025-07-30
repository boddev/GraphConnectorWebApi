# MCP Resources Implementation

This document describes the MCP (Model Context Protocol) Resources implementation for the SEC Edgar Graph Connector Web API.

## Overview

MCP Resources expose SEC Edgar documents as accessible resources that clients can read and reference. This implementation provides:

- Document resources with proper URIs following the MCP specification
- Resource metadata and schema definition
- Resource reading and content delivery capabilities
- Resource discovery and enumeration endpoints

## Resource URI Scheme

Resources use a custom URI scheme for SEC Edgar documents:

```
sec-edgar://documents/{cik}/{formType}/{filingDate}/{documentId}
```

**Example:**
```
sec-edgar://documents/0001144879/8-K/2025-04-30/bfda6f24f1972bbb3273f787574b3427ac75567242e9332624a0a69249224669
```

Where:
- `cik`: 10-digit SEC Central Index Key
- `formType`: SEC form type (e.g., 10-K, 10-Q, 8-K)
- `filingDate`: Filing date in YYYY-MM-DD format
- `documentId`: Unique document identifier hash

## API Endpoints

### Resource Discovery
- **GET** `/mcp/resources` - Discover available resources and endpoints

### Resource Listing
- **GET** `/mcp/resources/list` - List available resources with filtering

**Query Parameters:**
- `companyName` (string, optional): Filter by company name
- `formType` (string, optional): Filter by SEC form type
- `startDate` (date, optional): Start date for filing date range
- `endDate` (date, optional): End date for filing date range
- `limit` (int, optional): Number of results (1-1000, default: 100)
- `offset` (int, optional): Pagination offset (default: 0)
- `sortBy` (string, optional): Sort field (filingDate, companyName, formType, name)
- `sortOrder` (string, optional): Sort order (asc, desc, default: desc)

### Resource Content
- **GET** `/mcp/resources/content/{resourceUri}` - Get full resource content

### Resource Metadata
- **GET** `/mcp/resources/metadata/{resourceUri}` - Get resource metadata only

## Resource Schema

### McpResource
```json
{
  "uri": "string",
  "name": "string", 
  "description": "string",
  "mimeType": "string",
  "metadata": {
    "companyName": "string",
    "formType": "string",
    "filingDate": "datetime",
    "cik": "string",
    "ticker": "string",
    "contentLength": "number",
    "lastModified": "datetime",
    "edgarUrl": "string",
    "documentType": "string"
  }
}
```

### ResourceContent
```json
{
  "uri": "string",
  "mimeType": "string",
  "content": "string",
  "size": "number",
  "metadata": "ResourceMetadata"
}
```

## Example Usage

### Discover Resources
```bash
curl "http://localhost:5000/mcp/resources"
```

### List All Resources
```bash
curl "http://localhost:5000/mcp/resources/list?limit=10"
```

### Filter by Company
```bash
curl "http://localhost:5000/mcp/resources/list?companyName=Apple&limit=5"
```

### Filter by Form Type
```bash
curl "http://localhost:5000/mcp/resources/list?formType=10-K&limit=5"
```

### Get Resource Metadata
```bash
curl "http://localhost:5000/mcp/resources/metadata/sec-edgar%3A%2F%2Fdocuments%2F{cik}%2F{formType}%2F{date}%2F{documentId}"
```

### Get Resource Content
```bash
curl "http://localhost:5000/mcp/resources/content/sec-edgar%3A%2F%2Fdocuments%2F{cik}%2F{formType}%2F{date}%2F{documentId}"
```

## Implementation Details

### Components

1. **ResourceModels.cs**: Core data models for MCP resources
   - `McpResource`: Main resource representation
   - `ResourceMetadata`: SEC document-specific metadata
   - `ResourceContent`: Resource content wrapper
   - `ResourceUriScheme`: URI creation and parsing utilities

2. **ResourceService.cs**: Main service for resource management
   - Resource listing with filtering and pagination
   - Resource content retrieval by URI
   - Resource metadata retrieval
   - Document-to-resource conversion

3. **Program.cs**: API endpoint registration
   - Resource discovery endpoint
   - Resource listing endpoint
   - Resource content and metadata endpoints

### Integration

The MCP Resources implementation integrates with existing services:

- **DocumentSearchService**: Used for finding and retrieving SEC documents
- **Existing MCP Tools**: Shares infrastructure with document search tools
- **Storage Services**: Leverages existing storage abstraction layer

### Error Handling

All endpoints return standardized error responses:

```json
{
  "error": "Error message describing what went wrong"
}
```

Common error scenarios:
- Invalid resource URI format
- Resource not found
- Invalid query parameters
- Internal server errors

## MCP Specification Compliance

This implementation follows the MCP specification for resources:

- ✅ Resource URIs with custom scheme
- ✅ Resource metadata with proper schema
- ✅ Resource content delivery
- ✅ Resource discovery and enumeration
- ✅ Pagination and filtering support
- ✅ Standardized error responses