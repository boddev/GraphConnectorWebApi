# Quick Start Tutorial

Get up and running with the SEC Edgar MCP server in 10 minutes.

## Prerequisites

- **.NET 8 SDK** installed
- **Azure subscription** with admin access
- **Git** for cloning the repository

## Step 1: Clone and Build

```bash
# Clone the repository
git clone https://github.com/boddev/GraphConnectorWebApi.git
cd GraphConnectorWebApi

# Build the project
cd ApiGraphActivator
dotnet build
```

## Step 2: Create Azure AD App Registration

1. Go to [Azure Portal](https://portal.azure.com) → **Azure Active Directory** → **App registrations**

2. Click **New registration**:
   - **Name**: `SEC-Edgar-MCP-Server`
   - **Account types**: Accounts in this organizational directory only
   - **Redirect URI**: Leave blank

3. After creation, note these values from the **Overview** page:
   - **Application (client) ID**
   - **Directory (tenant) ID**

4. Go to **API permissions** → **Add a permission**:
   - Select **Microsoft Graph** → **Application permissions**
   - Add: `ExternalConnection.ReadWrite.OwnedBy`
   - Add: `ExternalItem.ReadWrite.OwnedBy`
   - Click **Grant admin consent for [your org]**

5. Go to **Certificates & secrets** → **New client secret**:
   - Description: `MCP Server Secret`
   - Expires: 12 months
   - **Copy the secret value** (you won't see it again)

## Step 3: Configure the Server

Set up configuration using user secrets (recommended for development):

```bash
# Set required configuration
dotnet user-secrets set "AzureAd:ClientId" "your-client-id-from-step-2"
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret-from-step-2"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id-from-step-2"
dotnet user-secrets set "EmailAddress" "your-email@company.com"
```

Replace the placeholder values with your actual Azure AD values and email address.

## Step 4: Start the Server

```bash
# Run the server
dotnet run

# You should see output like:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:5236
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: https://localhost:7034
```

## Step 5: Verify Installation

### Test Basic Health

```bash
curl http://localhost:5236/health
# Expected: Healthy
```

### Test MCP Tools Discovery

```bash
curl http://localhost:5236/mcp/tools
```

Expected response:
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

## Step 6: Test Your First Search

### Search for Apple Documents

```bash
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Apple Inc.",
    "formTypes": ["10-K"],
    "pageSize": 3
  }'
```

Expected response:
```json
{
  "content": {
    "items": [
      {
        "id": "apple-10k-2024-01-26",
        "title": "Apple Inc. 10-K 2024-01-26",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "filingDate": "2024-01-26T00:00:00",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/...",
        "contentPreview": "UNITED STATES SECURITIES AND EXCHANGE COMMISSION...",
        "relevanceScore": 1.0
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 3,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  },
  "isError": false,
  "errorMessage": null,
  "metadata": {
    "searchType": "company",
    "searchTerm": "Apple Inc.",
    "executionTime": "2024-01-15T10:30:00Z"
  }
}
```

## Step 7: Set Up Graph Connector (Optional)

If you want to also index documents into Microsoft 365:

### Grant Admin Permissions

```bash
curl -X POST http://localhost:5236/grantPermissions \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

This will redirect you to the Microsoft admin consent page. Sign in as a global admin and grant consent.

### Provision the Connector

```bash
curl -X POST http://localhost:5236/provisionconnection \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

Expected response:
```json
{
  "status": "Success",
  "message": "Connection provisioned successfully",
  "connectionId": "secedgartextdataset"
}
```

### Load Initial Content

```bash
curl -X POST http://localhost:5236/loadcontent \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

This starts a background process to index documents. Check the server logs for progress.

## Step 8: Explore the API

### Test Content Search

```bash
curl -X POST http://localhost:5236/mcp/tools/content-search \
  -H "Content-Type: application/json" \
  -d '{
    "searchText": "artificial intelligence",
    "formTypes": ["10-K"],
    "pageSize": 5
  }'
```

### Test Form Filtering

```bash
curl -X POST http://localhost:5236/mcp/tools/form-filter \
  -H "Content-Type: application/json" \
  -d '{
    "formTypes": ["10-Q"],
    "companyNames": ["Apple Inc.", "Microsoft Corporation"],
    "startDate": "2023-01-01",
    "pageSize": 10
  }'
```

### Browse the Swagger UI

Open your browser and go to: `https://localhost:7034/swagger`

This provides an interactive interface to explore all available endpoints.

## Next Steps

Now that you have the MCP server running, you can:

### 1. Integrate with Your Application

Choose your preferred integration method:
- **Python**: Follow the [Python examples](../integration/python-examples.md)
- **Node.js**: Follow the [Node.js examples](../integration/nodejs-examples.md)
- **REST API**: Use the [OpenAPI specification](../api/mcp-tools-openapi.yaml)

### 2. Set Up Production Deployment

For production use:
- Review the [MCP Server Setup Guide](../deployment/mcp-server-setup.md)
- Configure [Azure Table Storage](../deployment/mcp-server-setup.md#3-azure-table-storage-production) for persistence
- Set up monitoring and alerting

### 3. Explore Advanced Features

- **Content Analysis**: Build content analysis pipelines
- **Compliance Monitoring**: Monitor specific companies and form types
- **AI Integration**: Use with Microsoft Copilot or other AI systems

## Common Issues

### "Connection refused" errors
- Check that the server is running: `dotnet run`
- Verify the correct port: `http://localhost:5236` or `https://localhost:7034`

### Authentication errors
- Verify Azure AD configuration
- Check that admin consent was granted
- Confirm client secret hasn't expired

### Empty search results
- Try searching for major companies: "Apple Inc.", "Microsoft Corporation"
- Check the date range if using date filters
- Verify internet connectivity to SEC servers

### Tool discovery returns empty array
- Check server startup logs for errors
- Verify all required configuration is set
- Ensure the server started successfully

## Getting Help

If you encounter issues:

1. **Check the logs**: Look for error messages in the console output
2. **Review configuration**: Ensure all required settings are configured
3. **Test connectivity**: Verify internet access and SEC API availability
4. **Consult documentation**: 
   - [Troubleshooting Guide](../troubleshooting/common-issues.md)
   - [FAQ](../troubleshooting/faq.md)
5. **Get support**: Create an issue on GitHub with details about your problem

## Sample Integration Code

### Python Quick Test

```python
import requests

# Test company search
response = requests.post('http://localhost:5236/mcp/tools/company-search', 
    json={
        'companyName': 'Apple Inc.',
        'formTypes': ['10-K'],
        'pageSize': 1
    })

if response.status_code == 200:
    data = response.json()
    if not data['isError']:
        print(f"Found {data['content']['totalCount']} documents")
        for doc in data['content']['items']:
            print(f"- {doc['title']} ({doc['filingDate'][:10]})")
    else:
        print(f"Error: {data['errorMessage']}")
else:
    print(f"HTTP Error: {response.status_code}")
```

### Node.js Quick Test

```javascript
const axios = require('axios');

async function testMCPServer() {
    try {
        const response = await axios.post('http://localhost:5236/mcp/tools/company-search', {
            companyName: 'Apple Inc.',
            formTypes: ['10-K'],
            pageSize: 1
        });

        const data = response.data;
        if (!data.isError) {
            console.log(`Found ${data.content.totalCount} documents`);
            data.content.items.forEach(doc => {
                console.log(`- ${doc.title} (${doc.filingDate.substring(0, 10)})`);
            });
        } else {
            console.error(`Error: ${data.errorMessage}`);
        }
    } catch (error) {
        console.error(`HTTP Error: ${error.message}`);
    }
}

testMCPServer();
```

## Congratulations!

You now have a fully functional SEC Edgar MCP server that can:

✅ **Search companies** by name with form type filtering  
✅ **Filter documents** by form type and date ranges  
✅ **Search content** within SEC filings with full-text search  
✅ **Integrate with applications** via standardized MCP tools  
✅ **Scale horizontally** for production workloads  
✅ **Connect to Microsoft 365** for Copilot integration (optional)

The server is ready for integration with AI agents, automation systems, and custom applications requiring SEC filing data access.