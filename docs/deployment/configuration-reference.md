# Configuration Reference

Complete reference for all configuration options available in the SEC Edgar MCP server.

## Environment Variables

### Required Configuration

| Variable | Type | Description | Example |
|----------|------|-------------|---------|
| `AzureAd:ClientId` | string | Azure AD app registration client ID | `5431975-9395-4350-929b-f195e9466370` |
| `AzureAd:ClientSecret` | string | Azure AD app registration client secret | `ABC123~very-secret-value` |
| `AzureAd:TenantId` | string | Azure AD tenant ID | `6984256-3102-4916-9c26-eb94f327f56d` |
| `EmailAddress` | string | Contact email for SEC requests (required by SEC) | `contact@yourcompany.com` |

### Optional Configuration

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `TableStorage` | string | In-memory | Azure Storage connection string |
| `CompanyTableName` | string | `trackedCompanies` | Table name for company tracking |
| `ProcessedTableName` | string | `processedFormsHTMLText` | Table name for processed documents |
| `BlobContainerName` | string | `processed-data-text` | Blob container for document storage |
| `OpenAIKey` | string | None | Azure OpenAI API key for content enhancement |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | string | None | Application Insights telemetry |

### Storage Backend Configuration

| Variable | Type | Description | Example |
|----------|------|-------------|---------|
| `STORAGE_TYPE` | string | Storage backend type: `InMemory`, `LocalFile`, `AzureTable` | `AzureTable` |
| `STORAGE_PATH` | string | Local file storage path (when using LocalFile) | `/data/sec-filings` |

## appsettings.json Configuration

### Development Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ApiGraphActivator.McpTools": "Debug",
      "ApiGraphActivator.Services": "Debug"
    }
  },
  "AllowedHosts": "*",
  "AzureAd": {
    "ClientId": "",
    "ClientSecret": "",
    "TenantId": ""
  },
  "EmailAddress": "",
  "TableStorage": "UseDevelopmentStorage=true",
  "CompanyTableName": "trackedCompanies",
  "ProcessedTableName": "processedFormsHTMLText",
  "BlobContainerName": "processed-data-text",
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"],
    "AllowedMethods": ["GET", "POST", "OPTIONS"],
    "AllowedHeaders": ["Content-Type", "Authorization"]
  }
}
```

### Production Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "ApiGraphActivator": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "your-domain.com",
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
  "OpenAIKey": "#{OpenAIKey}#",
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.com"],
    "AllowedMethods": ["GET", "POST"],
    "AllowedHeaders": ["Content-Type", "Authorization"]
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5236"
      },
      "Https": {
        "Url": "https://0.0.0.0:7034",
        "Certificate": {
          "Path": "/etc/ssl/certs/application.pfx",
          "Password": "#{CertificatePassword}#"
        }
      }
    }
  }
}
```

## Configuration Sections

### Azure AD Section

```json
{
  "AzureAd": {
    "ClientId": "your-app-client-id",
    "ClientSecret": "your-app-client-secret",
    "TenantId": "your-tenant-id",
    "Instance": "https://login.microsoftonline.com/",
    "Scopes": ["https://graph.microsoft.com/.default"]
  }
}
```

### Storage Configuration

```json
{
  "Storage": {
    "Type": "AzureTable",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "CompanyTableName": "trackedCompanies",
    "ProcessedTableName": "processedFormsHTMLText",
    "BlobContainerName": "processed-data-text",
    "CacheExpirationMinutes": 60
  }
}
```

### MCP Tools Configuration

```json
{
  "McpTools": {
    "MaxPageSize": {
      "CompanySearch": 1000,
      "FormFilter": 1000,
      "ContentSearch": 100
    },
    "DefaultPageSize": 50,
    "CacheToolDiscovery": true,
    "CacheExpirationMinutes": 1440
  }
}
```

### SEC API Configuration

```json
{
  "SecApi": {
    "BaseUrl": "https://www.sec.gov",
    "UserAgent": "#{EmailAddress}#",
    "RateLimiting": {
      "RequestsPerSecond": 5,
      "BurstSize": 10,
      "RetryAttempts": 3,
      "RetryDelaySeconds": 2
    },
    "Timeout": 30
  }
}
```

### Application Insights Configuration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...",
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true,
    "EnableDependencyTracking": true,
    "EnableRequestTracking": true,
    "EnableExceptionTracking": true,
    "CustomTelemetry": {
      "TrackToolExecutions": true,
      "TrackSearchQueries": true,
      "TrackPerformanceMetrics": true
    }
  }
}
```

### CORS Configuration

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://your-frontend.com"
    ],
    "AllowedMethods": ["GET", "POST", "OPTIONS"],
    "AllowedHeaders": [
      "Content-Type",
      "Authorization",
      "X-Requested-With"
    ],
    "AllowCredentials": false,
    "MaxAge": 86400
  }
}
```

### Kestrel Server Configuration

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5236"
      },
      "Https": {
        "Url": "https://0.0.0.0:7034",
        "Certificate": {
          "Path": "/path/to/certificate.pfx",
          "Password": "certificate-password"
        }
      }
    },
    "Limits": {
      "MaxConcurrentConnections": 100,
      "MaxRequestBodySize": 10485760,
      "RequestHeadersTimeout": "00:00:30"
    }
  }
}
```

## Environment-Specific Configuration

### Setting Environment Variables

#### Windows (PowerShell)
```powershell
$env:AzureAd__ClientId="your-client-id"
$env:AzureAd__ClientSecret="your-client-secret"
$env:AzureAd__TenantId="your-tenant-id"
$env:EmailAddress="contact@yourcompany.com"
$env:TableStorage="DefaultEndpointsProtocol=https;AccountName=..."
```

#### Linux/macOS (Bash)
```bash
export AzureAd__ClientId="your-client-id"
export AzureAd__ClientSecret="your-client-secret"
export AzureAd__TenantId="your-tenant-id"
export EmailAddress="contact@yourcompany.com"
export TableStorage="DefaultEndpointsProtocol=https;AccountName=..."
```

#### Docker Environment Variables
```dockerfile
ENV AzureAd__ClientId="your-client-id"
ENV AzureAd__ClientSecret="your-client-secret"
ENV AzureAd__TenantId="your-tenant-id"
ENV EmailAddress="contact@yourcompany.com"
ENV TableStorage="DefaultEndpointsProtocol=https;AccountName=..."
```

### User Secrets (Development)

```bash
cd ApiGraphActivator

# Set secrets
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "EmailAddress" "contact@yourcompany.com"

# List all secrets
dotnet user-secrets list

# Clear specific secret
dotnet user-secrets remove "AzureAd:ClientSecret"

# Clear all secrets
dotnet user-secrets clear
```

## Configuration Validation

### Startup Validation

The application validates configuration at startup and will fail to start if required settings are missing:

```csharp
// Required settings that are validated:
- AzureAd:ClientId
- AzureAd:ClientSecret  
- AzureAd:TenantId
- EmailAddress
```

### Health Check Configuration

```json
{
  "HealthChecks": {
    "Enabled": true,
    "Endpoints": {
      "Liveness": "/health/live",
      "Readiness": "/health/ready"
    },
    "Checks": {
      "AzureStorage": true,
      "SecApi": true,
      "ApplicationInsights": true
    }
  }
}
```

## Performance Configuration

### Background Tasks

```json
{
  "BackgroundTasks": {
    "MaxConcurrentTasks": 10,
    "QueueCapacity": 1000,
    "TaskTimeout": "00:05:00",
    "RetryAttempts": 3
  }
}
```

### Caching Configuration

```json
{
  "Caching": {
    "InMemory": {
      "DefaultExpirationMinutes": 60,
      "MaxCacheSize": "512MB"
    },
    "Distributed": {
      "Enabled": false,
      "ConnectionString": "redis-connection-string",
      "DefaultExpirationMinutes": 120
    }
  }
}
```

### Connection Pool Configuration

```json
{
  "ConnectionPools": {
    "HttpClient": {
      "MaxConnections": 100,
      "ConnectionIdleTimeout": "00:02:00",
      "RequestTimeout": "00:00:30"
    },
    "Storage": {
      "MaxConcurrentOperations": 50,
      "OperationTimeout": "00:01:00"
    }
  }
}
```

## Security Configuration

### HTTPS Configuration

```json
{
  "Https": {
    "Required": true,
    "Port": 7034,
    "Certificate": {
      "Path": "/etc/ssl/certs/application.pfx",
      "Password": "certificate-password",
      "Source": "File"
    },
    "Protocols": ["Tls12", "Tls13"],
    "RedirectHttpToHttps": true
  }
}
```

### API Security

```json
{
  "Security": {
    "AllowedHosts": ["your-domain.com"],
    "RequireHttps": true,
    "ForwardedHeaders": {
      "Enabled": true,
      "ForwardedProtoHeaderName": "X-Forwarded-Proto",
      "ForwardedHostHeaderName": "X-Forwarded-Host"
    },
    "RateLimiting": {
      "Enabled": true,
      "RequestsPerMinute": 1000,
      "BurstSize": 100
    }
  }
}
```

## Monitoring and Logging

### Structured Logging

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "ApiGraphActivator": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/sec-edgar-mcp/application-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600
        }
      }
    ]
  }
}
```

## Configuration Best Practices

### 1. Environment Separation

- Use different `appsettings.{Environment}.json` files
- Never commit secrets to source control
- Use Azure Key Vault for production secrets

### 2. Secret Management

```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUrl": "https://your-keyvault.vault.azure.net/",
    "ClientId": "keyvault-client-id",
    "ClientSecret": "keyvault-client-secret"
  }
}
```

### 3. Configuration Validation

```csharp
// Custom validation attributes
[Required]
[EmailAddress]
public string EmailAddress { get; set; }

[Required]
[Guid]
public string TenantId { get; set; }
```

### 4. Configuration Monitoring

```json
{
  "ConfigurationMonitoring": {
    "ReloadOnChange": true,
    "ReloadDelay": 2000,
    "TrackChanges": true
  }
}
```

## Troubleshooting Configuration

### Common Issues

1. **Invalid Azure AD configuration**:
   - Verify client ID format (GUID)
   - Check client secret expiration
   - Ensure tenant ID is correct

2. **Storage connection issues**:
   - Test connection string format
   - Verify storage account permissions
   - Check firewall rules

3. **CORS problems**:
   - Verify allowed origins
   - Check HTTP vs HTTPS mismatches
   - Ensure methods are allowed

### Configuration Testing

```bash
# Test configuration loading
dotnet run --environment Development --dry-run

# Validate specific settings
curl http://localhost:5236/health/config

# Check environment variables
env | grep -E "(AzureAd|EmailAddress|TableStorage)"
```

This comprehensive configuration reference covers all aspects of configuring the SEC Edgar MCP server for different environments and use cases.