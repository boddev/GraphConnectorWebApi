# MCP Server Setup and Configuration

Complete guide for setting up and configuring the SEC Edgar MCP server for production use.

## Prerequisites

### System Requirements
- **.NET 8 SDK** or runtime
- **Windows, Linux, or macOS**
- **Minimum 2GB RAM** (4GB+ recommended)
- **10GB+ disk space** for caching and storage

### Required Azure Services
- **Azure AD App Registration** (for Graph connector authentication)
- **Azure Storage Account** (optional, for production storage backend)
- **Application Insights** (optional, for monitoring)

## Environment Configuration

### Required Environment Variables

| Variable | Description | Required | Example |
|----------|-------------|----------|---------|
| `AzureAd:ClientId` | Azure AD app registration client ID | Yes | `5431975-9395-4350-929b-f195e9466370` |
| `AzureAd:ClientSecret` | Azure AD app registration client secret | Yes | `ABC123%^#$7_8snE26ZEU409L2~-LRf30dle` |
| `AzureAd:TenantId` | Azure AD tenant ID | Yes | `6984256-3102-4916-9c26-eb94f327f56d` |
| `EmailAddress` | Contact email for SEC requests | Yes | `your-email@company.com` |

### Optional Configuration

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `TableStorage` | Azure Table Storage connection string | In-memory | `DefaultEndpointsProtocol=https;AccountName=...` |
| `CompanyTableName` | Table name for tracked companies | `trackedCompanies` | `trackedCompanies` |
| `ProcessedTableName` | Table name for processed forms | `processedFormsHTMLText` | `processedFormsHTMLText` |
| `BlobContainerName` | Blob container for processed data | `processed-data-text` | `processed-data-text` |
| `OpenAIKey` | Azure OpenAI API key | None | `9a97227a9fc14b4386d427c1cee58276` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection | None | `InstrumentationKey=...` |

## Storage Backend Options

### 1. In-Memory Storage (Development)

**Use case**: Development, testing, proof of concept

**Configuration**: No additional setup required

**Pros**:
- Zero setup time
- Fast access
- No external dependencies

**Cons**:
- Data lost on restart
- Limited scalability
- Single instance only

### 2. Local File Storage (Single Node)

**Use case**: Single-node production, on-premise deployments

**Configuration**:
```bash
export STORAGE_TYPE="LocalFile"
export STORAGE_PATH="/data/sec-filings"
```

**Setup**:
```bash
# Create storage directory
sudo mkdir -p /data/sec-filings
sudo chown -R appuser:appuser /data/sec-filings
```

**Pros**:
- No cloud dependencies
- Fast local access
- Full control over data

**Cons**:
- Single point of failure
- Manual backup required
- Limited scalability

### 3. Azure Table Storage (Production)

**Use case**: Production, cloud deployments, high availability

**Configuration**:
```bash
export STORAGE_TYPE="AzureTable"
export TableStorage="DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"
```

**Setup**:
1. Create Azure Storage Account
2. Get connection string from Azure Portal
3. Tables will be created automatically

**Pros**:
- High availability
- Automatic scaling
- Built-in redundancy
- Global distribution

**Cons**:
- Cloud dependency
- Ongoing costs
- Network latency

## MCP Server Configuration

### 1. Development Setup

#### Using User Secrets (Recommended for Development)

```bash
cd ApiGraphActivator

# Set required secrets
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "EmailAddress" "your-email@company.com"

# Optional: Add storage configuration
dotnet user-secrets set "TableStorage" "your-storage-connection-string"
dotnet user-secrets set "OpenAIKey" "your-openai-key"
```

#### Using appsettings.Development.json

```json
{
  "AzureAd": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id"
  },
  "EmailAddress": "your-email@company.com",
  "TableStorage": "UseDevelopmentStorage=true",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ApiGraphActivator.McpTools": "Debug"
    }
  }
}
```

### 2. Production Setup

#### Using Environment Variables

```bash
# Linux/macOS
export AzureAd__ClientId="your-client-id"
export AzureAd__ClientSecret="your-client-secret"
export AzureAd__TenantId="your-tenant-id"
export EmailAddress="your-email@company.com"
export TableStorage="your-storage-connection-string"

# Windows PowerShell
$env:AzureAd__ClientId="your-client-id"
$env:AzureAd__ClientSecret="your-client-secret"
$env:AzureAd__TenantId="your-tenant-id"
$env:EmailAddress="your-email@company.com"
$env:TableStorage="your-storage-connection-string"
```

#### Using appsettings.Production.json

```json
{
  "AzureAd": {
    "ClientId": "#{AzureAd.ClientId}#",
    "ClientSecret": "#{AzureAd.ClientSecret}#",
    "TenantId": "#{AzureAd.TenantId}#"
  },
  "EmailAddress": "#{EmailAddress}#",
  "TableStorage": "#{TableStorage}#",
  "CompanyTableName": "trackedCompanies",
  "ProcessedTableName": "processedFormsHTMLText",
  "BlobContainerName": "processed-data-text",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "ApiGraphActivator": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

## Azure AD App Registration Setup

### 1. Create App Registration

1. Go to **Azure Portal** → **Azure Active Directory** → **App registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `SEC-Edgar-MCP-Server`
   - **Account types**: Accounts in this organizational directory only
   - **Redirect URI**: Leave blank
4. Click **Register**

### 2. Configure API Permissions

1. Go to **API permissions** → **Add a permission**
2. Select **Microsoft Graph** → **Application permissions**
3. Add permissions:
   - `ExternalConnection.ReadWrite.OwnedBy`
   - `ExternalItem.ReadWrite.OwnedBy`
4. Click **Grant admin consent**

### 3. Create Client Secret

1. Go to **Certificates & secrets** → **New client secret**
2. Set description: `MCP Server Secret`
3. Choose expiration (12 months recommended)
4. **Copy the secret value immediately** (won't be shown again)

### 4. Note Required Values

From the app registration overview, note:
- **Application (client) ID**
- **Directory (tenant) ID**
- **Client secret value** (from previous step)

## Running the MCP Server

### 1. Development Mode

```bash
cd ApiGraphActivator
dotnet run
```

Server endpoints:
- **HTTP**: `http://localhost:5236`
- **HTTPS**: `https://localhost:7034`
- **Swagger UI**: `https://localhost:7034/swagger`
- **MCP Tools**: `http://localhost:5236/mcp/tools`

### 2. Production Mode

```bash
cd ApiGraphActivator
dotnet publish -c Release -o ./publish

# Run published version
cd publish
dotnet ApiGraphActivator.dll
```

### 3. Background Service

#### Systemd (Linux)

Create service file: `/etc/systemd/system/sec-edgar-mcp.service`

```ini
[Unit]
Description=SEC Edgar MCP Server
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/sec-edgar-mcp
ExecStart=/usr/bin/dotnet /opt/sec-edgar-mcp/ApiGraphActivator.dll
Restart=always
RestartSec=10
User=sec-edgar
Group=sec-edgar

# Environment variables
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=AzureAd__ClientId=your-client-id
Environment=AzureAd__ClientSecret=your-client-secret
Environment=AzureAd__TenantId=your-tenant-id
Environment=EmailAddress=your-email@company.com

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable sec-edgar-mcp
sudo systemctl start sec-edgar-mcp
sudo systemctl status sec-edgar-mcp
```

#### Windows Service

Using `dotnet publish` with Windows Service support:

```bash
dotnet publish -c Release -r win-x64 --self-contained
sc create "SEC Edgar MCP" binPath="C:\path\to\published\ApiGraphActivator.exe"
sc start "SEC Edgar MCP"
```

## Initial Setup and Verification

### 1. Grant Admin Permissions

```bash
curl -X POST http://localhost:5236/grantPermissions \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

This redirects to Microsoft admin consent flow.

### 2. Provision Graph Connector

```bash
curl -X POST http://localhost:5236/provisionconnection \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

### 3. Load Initial Content (Optional)

```bash
curl -X POST http://localhost:5236/loadcontent \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

### 4. Verify MCP Tools

```bash
curl http://localhost:5236/mcp/tools
```

Expected response:
```json
[
  {
    "name": "search_documents_by_company",
    "description": "Search SEC filing documents by company name",
    "endpoint": "/mcp/tools/company-search"
  },
  ...
]
```

### 5. Test Tool Execution

```bash
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "Apple Inc.",
    "formTypes": ["10-K"],
    "pageSize": 5
  }'
```

## Performance Tuning

### 1. Connection Pool Settings

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string;Max Pool Size=100;"
  }
}
```

### 2. Caching Configuration

```json
{
  "Caching": {
    "DefaultExpirationMinutes": 60,
    "ToolDiscoveryExpirationMinutes": 1440,
    "SearchResultsExpirationMinutes": 30
  }
}
```

### 3. Background Task Configuration

```json
{
  "BackgroundTasks": {
    "MaxConcurrentTasks": 10,
    "QueueCapacity": 1000
  }
}
```

### 4. Rate Limiting

```json
{
  "RateLimiting": {
    "SECEdgar": {
      "RequestsPerSecond": 5,
      "BurstSize": 10
    }
  }
}
```

## Security Configuration

### 1. HTTPS Configuration

#### Development Certificate
```bash
dotnet dev-certs https --trust
```

#### Production Certificate
```json
{
  "Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "/path/to/certificate.pfx",
        "Password": "certificate-password"
      }
    }
  }
}
```

### 2. CORS Configuration

```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-frontend-domain.com"],
    "AllowedMethods": ["GET", "POST"],
    "AllowedHeaders": ["Content-Type", "Authorization"]
  }
}
```

### 3. API Security

```json
{
  "Security": {
    "RequireHttps": true,
    "AllowedHosts": ["your-domain.com"],
    "ForwardedHeaders": {
      "ForwardedProtoHeaderName": "X-Forwarded-Proto"
    }
  }
}
```

## Health Checks and Monitoring

### 1. Health Check Endpoints

Built-in health checks available at:
- `/health` - Overall health
- `/health/ready` - Readiness probe
- `/health/live` - Liveness probe

### 2. Application Insights Integration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...",
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true
  }
}
```

### 3. Custom Metrics

Monitor these key metrics:
- Tool execution times
- Storage operation latency
- SEC API rate limit usage
- Memory usage patterns
- Error rates by tool type

## Backup and Recovery

### 1. Azure Storage Backup

```bash
# Export table data
az storage table export --account-name youraccount --table-name trackedCompanies

# Backup blob container
az storage blob copy start-batch --account-name youraccount \
  --source-container processed-data-text \
  --destination-container backup-processed-data-text
```

### 2. Local File System Backup

```bash
# Create backup
tar -czf sec-edgar-backup-$(date +%Y%m%d).tar.gz /data/sec-filings

# Automated backup script
#!/bin/bash
BACKUP_DIR="/backups"
DATA_DIR="/data/sec-filings"
DATE=$(date +%Y%m%d_%H%M%S)

tar -czf "$BACKUP_DIR/sec-edgar-$DATE.tar.gz" "$DATA_DIR"
find "$BACKUP_DIR" -name "sec-edgar-*.tar.gz" -mtime +7 -delete
```

## Troubleshooting Common Setup Issues

### 1. Authentication Failures

**Symptoms**: HTTP 401 errors, authentication exceptions

**Solutions**:
- Verify Azure AD app registration permissions
- Check client secret expiration
- Ensure admin consent was granted
- Validate tenant ID format

### 2. Storage Connection Issues

**Symptoms**: Storage exceptions, timeout errors

**Solutions**:
- Test storage connection string
- Check firewall rules
- Verify storage account permissions
- Test network connectivity

### 3. Performance Issues

**Symptoms**: Slow response times, timeouts

**Solutions**:
- Monitor Application Insights metrics
- Check storage backend performance
- Review SEC API rate limiting
- Optimize query patterns

### 4. Memory Issues

**Symptoms**: Out of memory exceptions, high memory usage

**Solutions**:
- Monitor garbage collection
- Review large object allocation
- Configure appropriate page sizes
- Add memory limits to queries

## Next Steps

1. **Load Balancing**: Set up multiple instances behind a load balancer
2. **CI/CD Pipeline**: Automate deployment with Azure DevOps or GitHub Actions
3. **Monitoring**: Set up comprehensive monitoring and alerting
4. **Scaling**: Configure auto-scaling based on demand
5. **Security**: Implement additional security measures for production

## Configuration Validation Checklist

- [ ] Azure AD app registration created and configured
- [ ] Required environment variables set
- [ ] Storage backend configured and accessible
- [ ] MCP server starts successfully
- [ ] Tool discovery endpoint responds
- [ ] Basic tool execution works
- [ ] Authentication flow completes
- [ ] Graph connector can be provisioned
- [ ] Health checks return success
- [ ] Logging and monitoring configured