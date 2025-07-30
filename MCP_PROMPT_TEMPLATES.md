# MCP Prompt Templates

This document describes the MCP (Model Context Protocol) prompt templates implemented for the SEC Edgar Graph Connector Web API.

## Overview

The MCP Prompt Templates provide reusable, parameterized prompt templates for common document analysis and AI interaction patterns. These templates follow the MCP specification and integrate seamlessly with the existing document search tools and AI analysis capabilities.

## Features

### ✅ Implemented (MCP-302)
- **Document analysis prompt templates** - Comprehensive templates for SEC filing analysis
- **Financial data extraction templates** - Structured templates for extracting financial data
- **Comparison and summarization templates** - Templates for comparative analysis
- **Template parameterization and customization** - Full parameter validation and substitution
- **MCP `/prompts/list` endpoint** - Standards-compliant prompt discovery
- **Template engine for prompt customization** - Advanced templating with conditionals and loops
- **Template validation and testing** - Comprehensive parameter validation
- **AI analysis tool integration** - Integration with OpenAI service for real analysis

## MCP Specification Compliance

### Standard Endpoints

#### 1. Prompts List (`GET /mcp/prompts/list`)
```bash
curl -X GET "http://localhost:5236/mcp/prompts/list"
```

Lists all available prompt templates in MCP-compliant format with:
- Prompt names and descriptions
- Required and optional arguments
- Cursor-based pagination support

#### 2. Prompts Get (`POST /mcp/prompts/get`)
```bash
curl -X POST "http://localhost:5236/mcp/prompts/get" \
  -H "Content-Type: application/json" \
  -d '{"name": "document-summary", "arguments": {"companyName": "Apple Inc.", ...}}'
```

Retrieves a specific prompt template with optional parameter rendering.

## Built-in Template Categories

### Document Analysis (`document-analysis`)
- **`document-summary`** - Generate comprehensive SEC filing summaries
- **`risk-assessment`** - Identify and assess risks in SEC filings

### Financial Extraction (`financial-extraction`) 
- **`financial-data-extraction`** - Extract specific financial data points with structured JSON output

### Comparison & Summarization (`comparison-summarization`)
- **`company-comparison`** - Compare financial and business metrics between companies
- **`quarterly-trend-analysis`** - Analyze trends across multiple quarterly reports

## Template Features

### Advanced Parameter System
- **Type validation** - String, Number, Boolean, Date, Array, Object types
- **Required/optional parameters** - Flexible parameter requirements
- **Default values** - Automatic fallback values for optional parameters
- **Validation rules** - Min/max length, patterns, value ranges
- **Allowed values** - Enumerated value constraints

### Template Engine Capabilities
- **Parameter substitution** - `{{parameterName}}` syntax
- **Conditional blocks** - `{{#if condition}}...{{/if}}` for optional content
- **Loop iteration** - `{{#each array}}...{{/each}}` for repeating content
- **Context variables** - `{{this}}`, `{{@index}}` for iteration context

### Example Template Structure
```json
{
  "name": "document-summary",
  "description": "Generate a comprehensive summary of SEC filing documents",
  "category": "document-analysis",
  "template": "You are a financial analyst...\n\nCompany: {{companyName}}\n{{#if includeMetrics}}Include detailed metrics{{/if}}",
  "parameters": [
    {
      "name": "companyName",
      "description": "Name of the company",
      "type": "String",
      "required": true
    },
    {
      "name": "includeMetrics", 
      "description": "Whether to include financial metrics",
      "type": "Boolean",
      "required": false,
      "defaultValue": "false"
    }
  ],
  "tags": ["summary", "analysis", "sec-filing"],
  "version": "1.0.0"
}
```

## Extended Management Endpoints

### Template Management
- **`GET /mcp/prompts/templates`** - List all templates with full metadata
- **`GET /mcp/prompts/templates?category=financial-extraction`** - Filter by category

### Template Operations
- **`POST /mcp/prompts/render`** - Render template with parameters
- **`POST /mcp/prompts/validate`** - Validate parameters against template

### AI Analysis Integration
- **`POST /mcp/prompts/analyze`** - Analyze document with template + AI
- **`GET /mcp/prompts/suggestions`** - Get recommended templates for document type
- **`POST /mcp/prompts/batch-analyze`** - Batch analysis of multiple documents

## Usage Examples

### 1. Get Analysis Suggestions for Document Type
```bash
curl -X GET "http://localhost:5236/mcp/prompts/suggestions?documentType=10-K&companyName=Apple%20Inc."
```

Returns relevance-scored template recommendations:
```json
{
  "suggestions": [
    {
      "templateName": "document-summary",
      "description": "Generate a comprehensive summary of SEC filing documents", 
      "category": "document-analysis",
      "relevanceScore": 1.0,
      "reason": "Ideal for generating comprehensive summaries of 10-K filings"
    }
  ]
}
```

### 2. Render Template with Parameters
```bash
curl -X POST "http://localhost:5236/mcp/prompts/render" \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "risk-assessment",
    "parameters": {
      "companyName": "Apple Inc.",
      "documentType": "10-K", 
      "filingDate": "2024-01-15",
      "documentContent": "Company faces various risks..."
    }
  }'
```

### 3. Analyze Document with AI
```bash
curl -X POST "http://localhost:5236/mcp/prompts/analyze?templateName=document-summary" \
  -H "Content-Type: application/json" \
  -d '{
    "documentId": "apple-10k-2024",
    "companyName": "Apple Inc.",
    "documentType": "10-K",
    "documentContent": "Full document content...",
    "filingDate": "2024-01-15"
  }'
```

### 4. Validate Template Parameters
```bash
curl -X POST "http://localhost:5236/mcp/prompts/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "document-summary",
    "parameters": {
      "documentType": "10-K",
      "companyName": "Apple Inc."
    }
  }'
```

Returns validation results:
```json
{
  "isValid": false,
  "errors": [
    "Required parameter 'filingDate' is missing",
    "Required parameter 'documentContent' is missing"
  ],
  "warnings": []
}
```

## Integration with Existing Tools

### Document Search Integration
Templates can be combined with existing MCP document search tools:
1. Search for documents using `/mcp/tools/company-search`
2. Get template suggestions using `/mcp/prompts/suggestions` 
3. Analyze documents using `/mcp/prompts/analyze`

### AI Service Integration
- **OpenAI Service** - Automatic integration when API key is configured
- **Graceful fallback** - Works without AI service for template operations
- **Error handling** - Robust error handling for AI service failures

## Template Development

### Adding Custom Templates
Templates can be added programmatically via the `PromptTemplateService`:

```csharp
var template = new PromptTemplate
{
    Name = "custom-analysis",
    Description = "Custom analysis template",
    Category = PromptCategories.DocumentAnalysis,
    Template = "Analyze {{documentType}} for {{companyName}}...",
    Parameters = new List<PromptParameter>
    {
        new() { Name = "documentType", Type = PromptParameterType.String, Required = true },
        new() { Name = "companyName", Type = PromptParameterType.String, Required = true }
    }
};

promptTemplateService.AddTemplate(template);
```

### Template Best Practices
1. **Clear descriptions** - Provide detailed template and parameter descriptions
2. **Proper categorization** - Use standard categories for discoverability
3. **Parameter validation** - Define appropriate validation rules
4. **Default values** - Provide sensible defaults for optional parameters
5. **Examples** - Include usage examples in template metadata
6. **Version control** - Use semantic versioning for template updates

## Error Handling

The prompt template system includes comprehensive error handling:

- **Template not found** - Returns 404 with clear error message
- **Invalid parameters** - Returns 400 with validation errors
- **Missing required parameters** - Lists all missing parameters
- **Type validation** - Validates parameter types and formats
- **AI service errors** - Graceful handling of AI service failures

## Performance Considerations

- **Template caching** - Templates are cached in memory for fast access
- **Parameter validation** - Efficient validation with early exit on errors
- **Batch operations** - Support for analyzing multiple documents
- **Rate limiting** - Built-in delays for AI service calls to respect limits

## Security

- **Input validation** - All parameters are validated before processing
- **Template isolation** - Templates cannot access system resources
- **Safe rendering** - Template engine prevents code injection
- **Error sanitization** - Error messages don't expose sensitive information

This implementation provides a complete, production-ready prompt template system that meets all the requirements specified in MCP-302 while maintaining compatibility with existing MCP tools and following best practices for security and performance.