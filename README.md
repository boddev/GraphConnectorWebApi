# SEC Edgar Graph Connector Web API

## Overview

The SEC Edgar Graph Connector Web API is a comprehensive .NET 8 web application that creates a Microsoft Graph connector to extract, process, and index SEC (Securities and Exchange Commission) filing documents into Microsoft 365 search and Copilot experiences. This solution enables organizations to make SEC filings (10-K, 10-Q, and 8-K forms) searchable through Microsoft Search and accessible via Microsoft Copilot.

**🆕 NEW: MCP Server Integration** - This solution now includes a Model Context Protocol (MCP) server that provides structured tool-based access to SEC filing documents for AI agents and automation systems.

**🆕 NEW: React Frontend** - This solution also includes a modern React-based frontend that eliminates Azure dependencies and provides an intuitive interface for selecting companies and managing crawl operations.

## What This Solution Does

This Graph Connector:
- **Extracts** SEC filing data from the publicly available EDGAR database
- **Processes** and enriches the content using AI services (Azure OpenAI)
- **Indexes** the processed documents into Microsoft Graph as external content
- **Enables** searchability of SEC filings through Microsoft 365 Search and Copilot
- **Provides** structured metadata including company information, filing dates, form types, and content
- **🆕 Exposes MCP Tools** for direct API access to SEC documents via standardized tools

### Supported SEC Form Types
- **10-K Reports**: Annual financial overviews providing comprehensive company performance data
- **10-Q Reports**: Quarterly financial snapshots with interim financial statements
- **8-K Forms**: Current reports notifying investors of significant company events

## Solution Components

### Backend API (.NET 8)
- RESTful API with Microsoft Graph integration
- Background processing queue for long-running operations
- Comprehensive logging with Application Insights
- CORS-enabled for frontend communication
- **🆕 MCP Server**: Structured tool-based access via Model Context Protocol

### Frontend (React)
- **Company Selection Interface**: Search and select from 10,000+ SEC-registered companies
- **Real-time Filtering**: Filter by ticker symbol or company name
- **Bulk Operations**: Select all or individual companies for crawling
- **Crawl Management**: Trigger and monitor background crawl operations
- **Zero Azure Dependencies**: Uses in-memory storage instead of Azure Table Storage

### 🆕 MCP Tools
- **Company Search**: Find SEC documents by company name with filtering
- **Form Filter**: Filter documents by SEC form type and date ranges  
- **Content Search**: Full-text search within SEC filing content
- **OpenAPI Specification**: Complete API documentation for integration

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 16+ and npm
- Microsoft 365 tenant with admin access
- Azure subscription (for OpenAI services)

### Running the Application

1. **Start the Backend**:
   ```bash
   dotnet run --project ApiGraphActivator
   ```

2. **Start the Frontend**:
   ```bash
   cd frontend
   npm install
   npm start
   ```

3. **Access the Application**:
   - Frontend: http://localhost:3000
   - Backend API: https://localhost:7034
   - Swagger UI: https://localhost:7034/swagger
   - **🆕 MCP Tools**: http://localhost:5236/mcp/tools

## 🆕 MCP Server Quick Start

The Model Context Protocol server provides structured access to SEC documents:

```bash
# Discover available tools
curl http://localhost:5236/mcp/tools

# Search Apple documents
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Apple Inc.",
    "formTypes": ["10-K", "10-Q"],
    "pageSize": 5
  }'

# Search for AI-related content
curl -X POST http://localhost:5236/mcp/tools/content-search \
  -H "Content-Type: application/json" \
  -d '{
    "searchText": "artificial intelligence",
    "formTypes": ["10-K"],
    "pageSize": 5
  }'
```

**📖 Complete MCP Documentation**: See the [MCP Documentation](./docs/README.md) for comprehensive guides, API reference, and integration examples.

## Solution Architecture

### High-Level Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   SEC EDGAR     │    │  Graph Connector │    │  Microsoft 365  │
│   Database      │───▶│     Web API      │───▶│   Search &      │
│                 │    │                  │    │    Copilot      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │                          
                              ├─────────────────┐        
                              │                 ▼        
                       ┌──────────────────┐    ┌─────────────────┐
                       │  Azure Storage   │    │  MCP Clients    │
                       │  & Table Storage │    │  (AI Agents)    │
                       └──────────────────┘    └─────────────────┘
```

### Component Architecture

#### Core Services
1. **EdgarService**: Handles data extraction from SEC EDGAR database
2. **ConnectionService**: Manages Microsoft Graph connector lifecycle
3. **ContentService**: Processes and transforms content for indexing
4. **GraphService**: Provides authenticated Microsoft Graph client
5. **OpenAIService**: Enhances content using Azure OpenAI
6. **LoggingService**: Centralized logging with Application Insights
7. **🆕 DocumentSearchService**: Powers MCP tools for structured document access

#### Data Flow
1. **Extract**: Pull SEC filing data from EDGAR database
2. **Transform**: Process HTML content, extract metadata, enhance with AI
3. **Load**: Push structured data to Microsoft Graph as external items
4. **Index**: Make content searchable in Microsoft 365

#### Storage Components
- **Azure Table Storage**: Stores processed filing metadata and tracking information
- **Azure Blob Storage**: Caches processed content and intermediate data
- **Microsoft Graph**: Final indexed content accessible via Search and Copilot

### API Endpoints

The solution exposes several REST API endpoints:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | GET | Health check endpoint |
| `/grantPermissions` | POST | Initiates admin consent flow for Graph permissions |
| `/provisionconnection` | POST | Creates and configures the Graph connector |
| `/loadcontent` | POST | Triggers content extraction and indexing (background process) |
| **🆕 `/mcp/tools`** | **GET** | **Discovers available MCP tools** |
| **🆕 `/mcp/tools/company-search`** | **POST** | **Search documents by company name** |
| **🆕 `/mcp/tools/form-filter`** | **POST** | **Filter documents by form type and date** |
| **🆕 `/mcp/tools/content-search`** | **POST** | **Full-text search within document content** |

## Prerequisites

### 1. Development Environment
- **.NET 8 SDK**: Download from [Microsoft .NET](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** or **VS Code** with C# extension
- **Azure CLI** (optional): For Azure resource management

### 2. Azure Services Required

#### Azure Active Directory (Entra ID) App Registration
You need an app registration with the following configurations:

**Required Microsoft Graph Permissions:**
- `ExternalConnection.ReadWrite.OwnedBy` (Application)
- `ExternalItem.ReadWrite.OwnedBy` (Application)

**Steps to create:**
1. Navigate to [Azure Portal](https://portal.azure.com) → Azure Active Directory → App registrations
2. Click "New registration"
3. Configure:
   - **Name**: `SEC-Edgar-Graph-Connector`
   - **Account types**: Accounts in this organizational directory only
   - **Redirect URI**: Leave blank
4. After creation, note:
   - **Application (client) ID**
   - **Directory (tenant) ID**
5. Create a client secret:
   - Go to "Certificates & secrets" → "New client secret"
   - Note the secret **Value** (save immediately, it won't be shown again)

#### Azure Storage Account
Required for caching and processing:

1. Create new storage account in Azure Portal
2. Configuration:
   - **Performance**: Standard
   - **Redundancy**: LRS (or higher based on requirements)
   - **Access tier**: Hot
3. Note the **Connection String** from "Access keys" section

#### Application Insights (Optional but Recommended)
For monitoring and diagnostics:

1. Create Application Insights resource
2. Note the **Connection String**

### 3. Environment Configuration

The application requires the following environment variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `AzureAd:ClientId` | App registration client ID | `5431975-9395-4350-929b-f195e9466370` |
| `AzureAd:ClientSecret` | App registration client secret | `ABC123%^#$7_8snE26ZEU409L2~-LRf30dle` |
| `AzureAd:TenantId` | Azure AD tenant ID | `6984256-3102-4916-9c26-eb94f327f56d` |
| `TableStorage` | Azure Storage connection string | `DefaultEndpointsProtocol=https;AccountName=...` |
| `CompanyTableName` | Table for tracked companies | `trackedCompanies` |
| `ProcessedTableName` | Table for processed forms | `processedFormsHTMLText` |
| `BlobContainerName` | Blob container for processed data | `processed-data-text` |
| `EmailAddress` | Contact email for SEC requests | `your-email@company.com` |
| `OpenAIKey` | Azure OpenAI API key (optional) | `9a97227a9fc14b4386d427c1cee58276` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection | `InstrumentationKey=...` |

## Deployment Guide

### Local Development Setup

1. **Clone the repository**:
   ```powershell
   git clone https://github.com/boddev/GraphConnectorWebApi.git
   cd GraphConnectorWebApi
   ```

2. **Configure User Secrets** (recommended for development):
   ```powershell
   cd ApiGraphActivator
   dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
   dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
   dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
   dotnet user-secrets set "TableStorage" "your-storage-connection-string"
   dotnet user-secrets set "EmailAddress" "your-email@company.com"
   # Add other required secrets...
   ```

3. **Build and run**:
   ```powershell
   dotnet build
   dotnet run
   ```

4. **Access Swagger UI**: Navigate to `http://localhost:5236/swagger`

### Production Deployment Options

#### Option 1: Azure App Service

1. **Create App Service**:
   ```bash
   az webapp create --resource-group myResourceGroup --plan myAppServicePlan --name sec-edgar-connector --runtime "DOTNETCORE:8.0"
   ```

2. **Configure App Settings**:
   ```bash
   az webapp config appsettings set --resource-group myResourceGroup --name sec-edgar-connector --settings \
     AzureAd__ClientId="your-client-id" \
     AzureAd__ClientSecret="your-client-secret" \
     AzureAd__TenantId="your-tenant-id" \
     TableStorage="your-storage-connection-string"
   ```

3. **Deploy**:
   ```powershell
   dotnet publish -c Release
   # Use Azure DevOps, GitHub Actions, or direct deployment
   ```

#### Option 2: Docker Container

1. **Build Docker image**:
   ```powershell
   docker build -t sec-edgar-connector .
   ```

2. **Run container**:
   ```powershell
   docker run -d -p 8080:8080 \
     -e AzureAd__ClientId="your-client-id" \
     -e AzureAd__ClientSecret="your-client-secret" \
     -e AzureAd__TenantId="your-tenant-id" \
     -e TableStorage="your-storage-connection-string" \
     sec-edgar-connector
   ```

#### Option 3: Azure Container Instances

```bash
az container create \
  --resource-group myResourceGroup \
  --name sec-edgar-connector \
  --image your-registry/sec-edgar-connector:latest \
  --environment-variables \
    AzureAd__ClientId="your-client-id" \
    AzureAd__TenantId="your-tenant-id" \
  --secure-environment-variables \
    AzureAd__ClientSecret="your-client-secret" \
    TableStorage="your-storage-connection-string"
```

## Usage Guide

### Step 1: Grant Admin Permissions

Before using the connector, an admin must consent to the required Graph permissions:

```http
POST /grantPermissions
Content-Type: application/json

"your-tenant-id"
```

This will redirect to the Microsoft admin consent flow.

### Step 2: Provision the Graph Connector

Create the connector and its schema in Microsoft Graph:

```http
POST /provisionconnection
Content-Type: application/json

"your-tenant-id"
```

This operation:
- Creates the external connection with ID `secedgartextdataset`
- Defines the schema for SEC filing metadata
- Configures search and display properties

### Step 3: Load Content

Trigger the content extraction and indexing process:

```http
POST /loadcontent
Content-Type: application/json

"your-tenant-id"
```

This starts a background process that:
- Extracts SEC filings from the EDGAR database
- Processes and enriches content
- Indexes items into Microsoft Graph
- Returns HTTP 202 (Accepted) immediately

### Step 4: Monitor Progress

Check Application Insights or application logs to monitor the indexing progress.

### Step 5: Search Content

Once indexed, SEC filings will be searchable through:
- **Microsoft Search** in Office 365
- **Microsoft Copilot** for contextual queries
- **SharePoint Search**

## Configuration Details

### Connection Configuration

The `ConnectionConfiguration.cs` defines the Graph connector properties:

- **Connection ID**: `secedgartextdataset`
- **Name**: `SECEdgarTextDataset`
- **Description**: Comprehensive description of SEC filing types
- **Schema**: Defines searchable properties (Title, Company, URL, DateFiled, Form)

### Content Schema

Each indexed item includes:

| Property | Type | Description |
|----------|------|-------------|
| Title | String | Filing title/description |
| Company | String | Company name |
| Url | String | Link to original SEC filing |
| DateFiled | DateTime | Filing date |
| Form | String | SEC form type (10-K, 10-Q, 8-K) |
| Content | HTML | Processed filing content |

### Background Processing

The solution uses a background task queue for long-running operations:
- **Bounded Channel**: Prevents memory overflow
- **Hosted Service**: Processes queued tasks
- **Graceful Shutdown**: Handles application termination

## Monitoring and Troubleshooting

### Application Insights Integration

The solution includes comprehensive telemetry:
- **Request tracking**: API endpoint usage
- **Dependency tracking**: External service calls
- **Custom events**: Business logic milestones
- **Exception tracking**: Error analysis

### Common Issues and Solutions

#### 1. Authentication Failures
**Symptoms**: 401 Unauthorized errors
**Solutions**:
- Verify app registration permissions
- Check client secret expiration
- Ensure admin consent was granted

#### 2. Storage Connection Issues
**Symptoms**: Storage-related exceptions
**Solutions**:
- Validate storage connection string
- Check storage account permissions
- Verify container/table existence

#### 3. SEC EDGAR Rate Limiting
**Symptoms**: HTTP 429 responses
**Solutions**:
- Implement exponential backoff (built-in)
- Verify User-Agent header configuration
- Monitor request frequency

#### 4. Graph Connector Provisioning Fails
**Symptoms**: Connection creation errors
**Solutions**:
- Verify Graph permissions
- Check for existing connections with same ID
- Review schema configuration

### Logs and Diagnostics

Key log categories:
- `ApiGraphActivator.Services.EdgarService`: SEC data extraction
- `ApiGraphActivator.Services.ConnectionService`: Graph connector operations
- `ApiGraphActivator.Services.ContentService`: Content processing

## Security Considerations

### Data Protection
- **Encryption in Transit**: HTTPS endpoints
- **Encryption at Rest**: Azure Storage encryption
- **Secrets Management**: Azure Key Vault integration recommended

### Access Control
- **App Registration**: Least privilege permissions
- **Service Principal**: Dedicated identity for Graph operations
- **Network Security**: Consider VNet integration for production

### Compliance
- **Data Residency**: Configure Azure regions appropriately
- **Audit Logging**: Application Insights provides audit trail
- **Data Retention**: Configure based on organizational policies

## Performance Optimization

### Scaling Considerations
- **Background Processing**: Increase queue capacity for high volume
- **Concurrent Processing**: Configure parallel operations
- **Caching**: Implement Redis cache for frequently accessed data

### Resource Management
- **Memory Usage**: Monitor for large document processing
- **Storage Costs**: Implement data lifecycle policies
- **API Throttling**: Respect Microsoft Graph rate limits

## Development and Extension

### Adding New SEC Form Types
1. Update `EdgarService` extraction logic
2. Modify `ConnectionConfiguration` schema if needed
3. Enhance `ContentService` transformation rules

### Custom Content Enhancement
The solution supports content enrichment via:
- **Azure OpenAI Integration**: Summarization, classification
- **Custom Processing Rules**: Industry-specific extraction
- **Metadata Enrichment**: Additional data sources

### Testing
```powershell
# Run unit tests
dotnet test

# Run integration tests
dotnet test --filter Category=Integration

# Performance testing
dotnet run --configuration Release --launch-profile Performance
```

## Support and Contributing

### Getting Help
- Check Application Insights for runtime issues
- Review logs for detailed error information
- Consult Microsoft Graph documentation for connector limitations

### Contributing
1. Fork the repository
2. Create feature branch
3. Submit pull request with tests
4. Follow established coding patterns

## License

This project is licensed under the MIT License. See LICENSE file for details.

## Additional Resources

### 📖 MCP Server Documentation
- [**Complete MCP Documentation**](./docs/README.md) - Comprehensive guides and API reference
- [**Quick Start Tutorial**](./docs/examples/quick-start.md) - Get started in 10 minutes
- [**OpenAPI Specification**](./docs/api/mcp-tools-openapi.yaml) - Complete API documentation
- [**Client Integration Examples**](./docs/integration/client-integration.md) - Python, Node.js, and REST examples
- [**Deployment Guide**](./docs/deployment/mcp-server-setup.md) - Production setup and configuration
- [**Troubleshooting**](./docs/troubleshooting/common-issues.md) - Common issues and solutions

### Microsoft Documentation
- [Microsoft Graph Connectors Documentation](https://docs.microsoft.com/en-us/microsoftsearch/connectors-overview)
- [SEC EDGAR Database](https://www.sec.gov/edgar/searchedgar/companysearch.html)
- [Azure Storage Documentation](https://docs.microsoft.com/en-us/azure/storage/)
- [Application Insights Documentation](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)

---

**Note**: This solution is designed for demonstration and proof-of-concept purposes. For production use, implement additional security measures, error handling, and monitoring based on your organization's requirements.