# Content Processing Tools - MCP Implementation

This document describes the content processing tools implemented as MCP (Model Context Protocol) tools for document analysis, summarization, and data extraction.

## Overview

The implementation adds 5 new MCP tools that integrate the existing PdfProcessingService with AI-powered analysis capabilities:

1. **Document Summarization Tool** - AI-powered document summaries
2. **Key Information Extraction Tool** - Structured data extraction  
3. **Financial Data Extraction Tool** - Financial statement analysis
4. **Cross-Document Relationship Tool** - Multi-document analysis
5. **PDF Processing Tool** - Text extraction from PDFs

## Tool Details

### 1. Document Summarization Tool
- **Endpoint**: `/mcp/tools/summarize-document`
- **Name**: `summarize_document`
- **Description**: Generate comprehensive AI-powered summaries with customizable length and focus areas
- **Features**:
  - Supports PDF URLs and direct text content
  - 5 summary length levels (brief to comprehensive)
  - Customizable focus areas
  - Key metrics extraction
  - Configurable page limits

### 2. Key Information Extraction Tool  
- **Endpoint**: `/mcp/tools/extract-key-information`
- **Name**: `extract_key_information`
- **Description**: Extract structured information including company details, financial data, legal info, and risk factors
- **Extraction Types**:
  - `company` - Company information (name, ticker, industry, etc.)
  - `financial` - Financial data (revenue, assets, liabilities, etc.)
  - `legal` - Legal information (proceedings, regulations, compliance)
  - `risk` - Risk factors and risk-related information
  - `all` - All of the above

### 3. Financial Data Extraction Tool
- **Endpoint**: `/mcp/tools/extract-financial-data` 
- **Name**: `extract_financial_data`
- **Description**: Extract detailed financial data including statements, metrics, ratios, and trends
- **Features**:
  - Financial statements parsing (Income, Balance Sheet, Cash Flow)
  - Key metrics extraction
  - Financial ratios calculation
  - Trend analysis
  - Multi-currency support

### 4. Cross-Document Relationship Analysis Tool
- **Endpoint**: `/mcp/tools/analyze-cross-document-relationships`
- **Name**: `analyze_cross_document_relationships`
- **Description**: Analyze relationships between multiple documents
- **Analysis Types**:
  - `timeline` - Chronological event tracking
  - `consistency` - Consistency analysis across documents
  - `evolution` - Change tracking over time
  - `relationships` - Document relationship mapping

### 5. PDF Processing Tool
- **Endpoint**: `/mcp/tools/process-pdf`
- **Name**: `process_pdf_document` 
- **Description**: Extract text content from PDF documents
- **Features**:
  - URL-based PDF processing
  - Base64 byte array support
  - Configurable page limits
  - Text cleaning and normalization
  - Document metrics (character/word counts)

## Technical Implementation

### Architecture
- Built on existing MCP tool infrastructure (`McpToolBase`)
- Integrates `PdfProcessingService` for PDF text extraction
- Uses `OpenAIService` for AI-powered analysis
- Follows established validation and error handling patterns

### Data Models
New models in `ContentProcessingModels.cs`:
- Parameter classes for each tool
- Result classes with structured data
- Supporting structures (DocumentInfo, CompanyInformation, etc.)
- Comprehensive data types for financial and legal information

### Dependencies
- **Azure OpenAI**: AI-powered analysis (requires `OpenAIKey` environment variable)
- **iText7**: PDF processing (existing dependency)
- **HttpClient**: URL-based document fetching
- **System.ComponentModel.DataAnnotations**: Parameter validation

### Error Handling
- Graceful handling of missing OpenAI API key
- Comprehensive parameter validation
- Detailed error messages for troubleshooting
- Proper HTTP status codes

## Usage Examples

### Document Summarization
```json
POST /mcp/tools/summarize-document
{
  "documentUrl": "https://example.com/document.pdf",
  "summaryLength": 3,
  "focusAreas": ["financial performance", "risk factors"],
  "includeKeyMetrics": true,
  "maxPages": 50
}
```

### Key Information Extraction
```json
POST /mcp/tools/extract-key-information
{
  "documentContent": "Document text content...",
  "extractionType": "all",
  "maxPages": 100
}
```

### Financial Data Extraction
```json
POST /mcp/tools/extract-financial-data
{
  "documentUrl": "https://example.com/10k.pdf",
  "extractionScope": ["revenue", "profit", "ratios"],
  "currency": "USD"
}
```

### Cross-Document Analysis
```json
POST /mcp/tools/analyze-cross-document-relationships
{
  "documentUrls": [
    "https://example.com/q1-2023.pdf",
    "https://example.com/q2-2023.pdf"
  ],
  "companyName": "Company XYZ",
  "analysisType": "timeline",
  "focusAreas": ["financial performance"]
}
```

### PDF Processing
```json
POST /mcp/tools/process-pdf
{
  "pdfUrl": "https://example.com/document.pdf",
  "maxPages": 10,
  "cleanText": true
}
```

## Configuration

### Environment Variables
- `OpenAIKey`: Azure OpenAI API key (required for AI-powered tools)

### Service Registration
Tools are automatically registered in `Program.cs` with proper dependency injection.

## Tool Discovery

All tools are discoverable via the MCP tools endpoint:
```
GET /mcp/tools
```

Returns complete tool schemas with input parameters and descriptions.

## Testing

Run the test script to verify functionality:
```bash
./test_content_processing_tools.sh
```

## Integration with M365 Copilot

These tools are designed to integrate with M365 Copilot Chat, providing advanced document processing capabilities for financial and legal document analysis. The structured output formats enable seamless integration with downstream systems and workflows.