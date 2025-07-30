# MCP Workflow Tools Documentation

## Overview

The MCP Workflow Tools provide comprehensive workflow orchestration capabilities for the SEC Edgar Graph Connector Web API. These tools enable users to create multi-step workflows that combine existing MCP tools (company search, form filtering, content search) into complex document processing operations.

## Key Features

### 1. Multi-Step Workflow Orchestration
- Define workflows with multiple sequential or parallel steps
- Dependency management between workflow steps
- Parameter substitution and variable interpolation
- Error handling with continue-on-error options

### 2. Background Execution
- Asynchronous workflow execution using existing background task queue
- Real-time progress tracking and status monitoring
- Estimated time remaining calculations
- Cancellation support for running workflows

### 3. Batch Processing
- Process multiple data items through the same workflow
- Configurable batch sizes and parallel execution limits
- Retry mechanisms for failed items
- Continue-on-error options for robust processing

### 4. Result Aggregation
- Consolidate results from multiple workflow executions
- Statistical analysis (success rates, duration metrics)
- Detailed and summary reporting options
- Custom aggregation types

### 5. Persistence and Storage
- File-based storage for workflow definitions and executions
- In-memory storage option for testing
- JSON-based configuration with human-readable format
- Workflow versioning and metadata tracking

## Available MCP Tools

### 1. Workflow Definition Tool (`workflow-definition`)

Creates and validates multi-step workflow definitions.

**Parameters:**
```json
{
  "workflow": {
    "name": "string (required)",
    "description": "string",
    "steps": [
      {
        "name": "string (required)",
        "description": "string",
        "toolName": "string (required)", // company-search, form-filter, content-search
        "parameters": "object",
        "dependsOn": ["array of step IDs"],
        "continueOnError": "boolean",
        "timeout": "string (ISO 8601 duration)"
      }
    ],
    "tags": ["array of strings"]
  }
}
```

**Example:**
```json
{
  "workflow": {
    "name": "Comprehensive Company Analysis",
    "description": "Search company documents, filter by form types, and analyze content",
    "steps": [
      {
        "name": "Search Company Documents",
        "toolName": "company-search",
        "parameters": {
          "companyName": "${workflow.companyName}",
          "maxResults": 100
        }
      },
      {
        "name": "Filter 10-K Forms",
        "toolName": "form-filter",
        "parameters": {
          "formTypes": ["10-K"],
          "startDate": "${workflow.startDate}",
          "endDate": "${workflow.endDate}"
        },
        "dependsOn": ["${steps.0.id}"]
      },
      {
        "name": "Search Risk Factors",
        "toolName": "content-search",
        "parameters": {
          "searchTerm": "risk factors",
          "companyFilter": "${workflow.companyName}"
        },
        "dependsOn": ["${steps.1.id}"]
      }
    ],
    "tags": ["company-analysis", "risk-assessment"]
  }
}
```

### 2. Workflow Execution Tool (`workflow-execution`)

Executes defined workflows with parameter substitution.

**Parameters:**
```json
{
  "workflowId": "string (required)",
  "parameters": "object",
  "initiatedBy": "string"
}
```

**Example:**
```json
{
  "workflowId": "7a516871-3fec-4e91-abb7-8cdad7c16178",
  "parameters": {
    "companyName": "Apple Inc",
    "startDate": "2023-01-01",
    "endDate": "2023-12-31"
  },
  "initiatedBy": "analyst@company.com"
}
```

### 3. Workflow Status Tool (`workflow-status`)

Monitors workflow execution progress and retrieves detailed status information.

**Parameters:**
```json
{
  "executionId": "string (required)",
  "includeStepDetails": "boolean"
}
```

**Response includes:**
- Overall workflow status (Pending, Running, Completed, Failed, Cancelled)
- Progress metrics (completion percentage, step counts)
- Estimated time remaining
- Individual step execution details
- Error information if applicable

### 4. Batch Processing Tool (`batch-processing`)

Executes workflows for multiple data items with configurable parallelism.

**Parameters:**
```json
{
  "workflowId": "string (required)",
  "items": [
    "array of parameter objects"
  ],
  "config": {
    "batchSize": "integer (1-1000)",
    "maxParallelism": "integer (1-50)",
    "retryCount": "integer (0-10)",
    "retryDelay": "string (ISO 8601 duration)",
    "continueOnError": "boolean"
  },
  "initiatedBy": "string"
}
```

**Example:**
```json
{
  "workflowId": "7a516871-3fec-4e91-abb7-8cdad7c16178",
  "items": [
    {"companyName": "Apple Inc"},
    {"companyName": "Microsoft Corporation"},
    {"companyName": "Amazon.com Inc"}
  ],
  "config": {
    "batchSize": 10,
    "maxParallelism": 3,
    "retryCount": 2,
    "retryDelay": "PT30S",
    "continueOnError": true
  },
  "initiatedBy": "batch-processor@company.com"
}
```

### 5. Result Aggregation Tool (`result-aggregation`)

Aggregates and analyzes results from multiple workflow executions.

**Parameters:**
```json
{
  "executionIds": ["array of execution IDs (required)"],
  "aggregationType": "string (summary|detailed|statistical)",
  "includeDetails": "boolean"
}
```

**Aggregation Types:**
- **Summary**: Basic metrics (success rate, total executions)
- **Detailed**: Includes execution breakdowns and step success rates
- **Statistical**: Adds duration statistics (min, max, average, median, standard deviation)

## Parameter Substitution

Workflows support variable interpolation using `${variable}` syntax:

### Workflow Parameters
```json
"parameters": {
  "searchTerm": "${workflow.keyword}"
}
```

### Step Results
```json
"parameters": {
  "companyName": "${stepId.result.companyName}"
}
```

### Step Dependencies
```json
"dependsOn": ["${steps.0.id}", "${steps.1.id}"]
```

## API Endpoints

### Workflow Management
- `GET /workflows` - List all workflow definitions
- `GET /workflows/{id}` - Get specific workflow definition
- `DELETE /workflows/{id}` - Delete workflow definition

### Execution Management
- `GET /workflow-executions` - List workflow executions
- `GET /workflow-executions/{id}` - Get specific execution
- `POST /workflow-executions/{id}/cancel` - Cancel running execution

### MCP Tool Endpoints
- `POST /mcp/tools/workflow-definition` - Create workflow
- `POST /mcp/tools/workflow-execution` - Execute workflow
- `POST /mcp/tools/workflow-status` - Get execution status
- `POST /mcp/tools/batch-processing` - Batch execute workflows
- `POST /mcp/tools/result-aggregation` - Aggregate results
- `GET /mcp/tools` - Discover all available tools

## Storage Configuration

Workflows are stored using the configurable storage system:

### File Storage (Default)
- Location: `workflow-data/` directory
- Format: JSON files with human-readable structure
- Persistent across application restarts
- Suitable for production use

### In-Memory Storage
- For testing and development
- Fast access but non-persistent
- Configurable via dependency injection

## Error Handling

### Workflow-Level Errors
- Invalid workflow definitions (circular dependencies, missing tools)
- Storage errors (permission issues, disk space)
- Service resolution failures

### Step-Level Errors
- MCP tool execution failures
- Parameter resolution errors
- Timeout violations
- Dependency failures

### Batch Processing Errors
- Individual item failures with continue-on-error support
- Batch size and parallelism limit enforcement
- Retry mechanisms with exponential backoff

## Performance Considerations

### Scalability
- Background execution prevents API blocking
- Configurable parallelism limits prevent resource exhaustion
- Batch processing optimizes throughput for large datasets

### Resource Management
- Workflow execution uses existing background task queue
- Memory usage scales with active workflow count
- File storage provides efficient persistence

### Monitoring
- Progress tracking with percentage completion
- Estimated time remaining calculations
- Detailed execution logging
- Status monitoring endpoints

## Best Practices

### Workflow Design
1. Use descriptive names and descriptions for workflows and steps
2. Design workflows with proper error handling
3. Leverage parameter substitution for reusable workflows
4. Use tags for workflow categorization and discovery

### Batch Processing
1. Configure appropriate batch sizes for your data volume
2. Set reasonable parallelism limits based on system capacity
3. Enable continue-on-error for robust batch operations
4. Monitor execution progress for large batches

### Error Handling
1. Design workflows with appropriate continue-on-error settings
2. Use step dependencies to control execution flow
3. Implement retry mechanisms for transient failures
4. Monitor execution logs for troubleshooting

### Performance
1. Use batch processing for multiple similar operations
2. Configure timeouts for long-running steps
3. Monitor system resources during large workflow executions
4. Use result aggregation for analysis and reporting

## Integration Examples

### Simple Document Search Workflow
```json
{
  "workflow": {
    "name": "Basic Document Search",
    "steps": [
      {
        "name": "Search Documents",
        "toolName": "company-search",
        "parameters": {
          "companyName": "${workflow.company}",
          "maxResults": 50
        }
      }
    ]
  }
}
```

### Complex Multi-Step Analysis
```json
{
  "workflow": {
    "name": "Comprehensive SEC Analysis",
    "steps": [
      {
        "name": "Find Company Documents",
        "toolName": "company-search",
        "parameters": {
          "companyName": "${workflow.companyName}",
          "maxResults": 200
        }
      },
      {
        "name": "Filter Recent 10-K Forms",
        "toolName": "form-filter",
        "parameters": {
          "formTypes": ["10-K"],
          "startDate": "${workflow.analysisStartDate}",
          "endDate": "${workflow.analysisEndDate}"
        },
        "dependsOn": ["${steps.0.id}"]
      },
      {
        "name": "Search Risk Disclosures",
        "toolName": "content-search",
        "parameters": {
          "searchTerm": "${workflow.riskKeywords}",
          "caseSensitive": false,
          "exactMatch": false
        },
        "dependsOn": ["${steps.1.id}"]
      },
      {
        "name": "Search Financial Metrics",
        "toolName": "content-search",
        "parameters": {
          "searchTerm": "${workflow.financialTerms}",
          "caseSensitive": false
        },
        "dependsOn": ["${steps.1.id}"]
      }
    ]
  }
}
```

This comprehensive workflow system enables sophisticated document processing operations while maintaining simplicity and reliability through the existing MCP tools architecture.