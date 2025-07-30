# Troubleshooting Guide

Common issues and solutions for the SEC Edgar MCP server.

## Quick Diagnostics

### Health Check Commands

```bash
# Basic server health
curl http://localhost:5236/health

# MCP tools availability
curl http://localhost:5236/mcp/tools

# Test basic tool execution
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{"companyName": "Apple", "pageSize": 1}'
```

### Log Analysis

```bash
# View recent logs (if using systemd)
journalctl -u sec-edgar-mcp -f

# View application logs
tail -f /var/log/sec-edgar-mcp/application.log

# Check for specific errors
grep "ERROR\|Exception" /var/log/sec-edgar-mcp/application.log
```

## Common Issues

### 1. Authentication and Authorization

#### Issue: HTTP 401 Unauthorized Errors

**Symptoms**:
- MCP tools return authentication errors
- Graph connector operations fail
- "Insufficient privileges" messages

**Diagnostic Steps**:
```bash
# Check Azure AD configuration
curl -X POST http://localhost:5236/grantPermissions \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

**Solutions**:

1. **Verify App Registration Permissions**:
   - Go to Azure Portal → Azure AD → App registrations
   - Check that these permissions are granted:
     - `ExternalConnection.ReadWrite.OwnedBy`
     - `ExternalItem.ReadWrite.OwnedBy`
   - Ensure admin consent was granted

2. **Check Client Secret Expiration**:
   ```bash
   # Look for authentication errors in logs
   grep "authentication\|ClientSecret" /var/log/sec-edgar-mcp/application.log
   ```
   - Go to Azure Portal → App registration → Certificates & secrets
   - Verify secret hasn't expired
   - Generate new secret if needed

3. **Validate Configuration**:
   ```bash
   # Check environment variables
   echo $AzureAd__ClientId
   echo $AzureAd__TenantId
   # Don't echo the secret for security
   ```

#### Issue: "Invalid tenant ID" Error

**Symptoms**:
- Authentication fails immediately
- Tenant-related error messages

**Solutions**:
1. Verify tenant ID format (GUID format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)
2. Get correct tenant ID from Azure Portal → Azure AD → Overview
3. Ensure no extra spaces or characters in configuration

### 2. Storage and Database Issues

#### Issue: Storage Connection Failures

**Symptoms**:
- "Storage connection failed" errors
- Data not persisting between restarts
- Timeout exceptions during operations

**Diagnostic Steps**:
```bash
# Test Azure Storage connection
az storage account show --name yourstorageaccount --resource-group yourresourcegroup

# Check local file storage permissions
ls -la /data/sec-filings
```

**Solutions**:

1. **Azure Storage Issues**:
   ```bash
   # Test connection string
   az storage table list --connection-string "your-connection-string"
   ```
   - Verify connection string format
   - Check storage account firewall rules
   - Ensure storage account is accessible from server location

2. **Local File Storage Issues**:
   ```bash
   # Fix permissions
   sudo chown -R appuser:appuser /data/sec-filings
   sudo chmod -R 755 /data/sec-filings
   
   # Check disk space
   df -h /data
   ```

3. **In-Memory Storage Issues**:
   - Data lost on restart (expected behavior)
   - Consider switching to persistent storage for production

#### Issue: Table/Container Creation Failures

**Symptoms**:
- "Table does not exist" errors
- "Container not found" messages

**Solutions**:
```bash
# Manually create Azure Storage tables
az storage table create --name trackedCompanies --connection-string "your-connection-string"
az storage table create --name processedFormsHTMLText --connection-string "your-connection-string"

# Create blob container
az storage container create --name processed-data-text --connection-string "your-connection-string"
```

### 3. MCP Tool Execution Issues

#### Issue: Tool Discovery Returns Empty Results

**Symptoms**:
- `/mcp/tools` endpoint returns `[]`
- Tools not being registered

**Diagnostic Steps**:
```bash
# Check server startup logs
grep "Tool\|MCP" /var/log/sec-edgar-mcp/application.log

# Verify server is running
netstat -tlnp | grep :5236
```

**Solutions**:
1. **Check Service Registration**:
   - Verify `DocumentSearchService` is properly registered in DI container
   - Ensure MCP tools are being discovered during startup

2. **Configuration Issues**:
   ```bash
   # Check if MCP endpoints are enabled
   curl -v http://localhost:5236/mcp/tools
   ```

#### Issue: "Invalid form types" Error

**Symptoms**:
- Tool execution returns validation errors
- Form type parameters rejected

**Example Error**:
```json
{
  "isError": true,
  "errorMessage": "Invalid form types: 10-k. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A"
}
```

**Solutions**:
1. **Use Correct Form Type Format**:
   ```json
   {
     "formTypes": ["10-K", "10-Q", "8-K"]  // Correct
   }
   ```
   Not:
   ```json
   {
     "formTypes": ["10-k", "10k", "10K"]   // Incorrect
   }
   ```

2. **Supported Form Types**:
   - `10-K` - Annual reports
   - `10-Q` - Quarterly reports
   - `8-K` - Current reports
   - `10-K/A` - Amended annual reports
   - `10-Q/A` - Amended quarterly reports
   - `8-K/A` - Amended current reports

#### Issue: Pagination Errors

**Symptoms**:
- "Page size too large" errors
- Inconsistent pagination results

**Solutions**:
1. **Respect Page Size Limits**:
   - Company/Form search: Max 1000 per page
   - Content search: Max 100 per page
   - Use reasonable page sizes (50-100 recommended)

2. **Proper Pagination Logic**:
   ```javascript
   // Correct pagination
   let page = 1;
   let hasMore = true;
   
   while (hasMore) {
     const result = await client.searchByCompany({
       companyName: "Apple",
       page: page,
       pageSize: 50
     });
     
     // Process results
     hasMore = result.content.hasNextPage;
     page++;
   }
   ```

### 4. Performance Issues

#### Issue: Slow Response Times

**Symptoms**:
- Tool execution takes >30 seconds
- Frequent timeout errors
- High CPU/memory usage

**Diagnostic Steps**:
```bash
# Monitor system resources
top -p $(pgrep -f "ApiGraphActivator")
htop

# Check network connectivity
ping sec.gov
curl -w "@curl-format.txt" -o /dev/null -s "https://www.sec.gov/Archives/edgar/daily-index/2024/QTR1/"
```

**Solutions**:

1. **Database Query Optimization**:
   ```json
   // Use date ranges to limit search scope
   {
     "companyName": "Apple",
     "startDate": "2023-01-01",
     "endDate": "2023-12-31"
   }
   ```

2. **Storage Backend Optimization**:
   - Use Azure Storage in same region as server
   - Configure appropriate connection pool sizes
   - Consider caching frequently accessed data

3. **SEC API Rate Limiting**:
   - Built-in rate limiting should handle this automatically
   - Monitor for rate limit exceeded errors
   - Consider implementing request queuing for high-volume scenarios

#### Issue: Memory Leaks or High Memory Usage

**Symptoms**:
- Memory usage continuously increases
- Out of memory exceptions
- Application becomes unresponsive

**Diagnostic Steps**:
```bash
# Monitor memory usage over time
watch -n 5 'ps -p $(pgrep -f "ApiGraphActivator") -o pid,ppid,cmd,%mem,%cpu --sort=-%mem'

# Check for memory dumps
ls -la /tmp/core.*
```

**Solutions**:
1. **Limit Page Sizes**:
   ```json
   {
     "pageSize": 50  // Instead of 1000
   }
   ```

2. **Disable Content Inclusion for Large Searches**:
   ```json
   {
     "includeContent": false  // Reduces memory usage
   }
   ```

3. **Restart Schedule**:
   ```bash
   # Add to crontab for nightly restart
   0 2 * * * systemctl restart sec-edgar-mcp
   ```

### 5. Network and Connectivity Issues

#### Issue: SEC EDGAR API Connectivity

**Symptoms**:
- "Unable to connect to SEC servers" errors
- Intermittent failures when fetching documents

**Diagnostic Steps**:
```bash
# Test SEC API connectivity
curl -H "User-Agent: Your-Company contact@company.com" \
  "https://www.sec.gov/Archives/edgar/daily-index/master.idx"

# Check DNS resolution
nslookup sec.gov
```

**Solutions**:
1. **User-Agent Header**:
   - SEC requires proper User-Agent header
   - Format: "Company-Name contact@company.com"
   - Configure in `EmailAddress` setting

2. **Firewall Rules**:
   ```bash
   # Allow outbound connections to SEC
   sudo ufw allow out 443/tcp
   sudo ufw allow out 80/tcp
   ```

3. **Proxy Configuration**:
   ```json
   {
     "HttpClient": {
       "Proxy": {
         "Address": "http://proxy.company.com:8080",
         "BypassOnLocal": true
       }
     }
   }
   ```

#### Issue: CORS Errors (Browser Clients)

**Symptoms**:
- Browser console shows CORS errors
- Requests blocked by browser

**Solutions**:
```json
// Configure CORS in appsettings.json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "https://your-frontend.com"],
    "AllowedMethods": ["GET", "POST", "OPTIONS"],
    "AllowedHeaders": ["Content-Type", "Authorization"]
  }
}
```

### 6. Configuration Issues

#### Issue: Environment Variables Not Loading

**Symptoms**:
- Default values being used instead of configured values
- Configuration-related errors

**Diagnostic Steps**:
```bash
# Check if environment variables are set
env | grep -i azure
env | grep -i email

# Test .NET configuration loading
dotnet run --project ApiGraphActivator -- --list-configuration
```

**Solutions**:
1. **Environment Variable Format**:
   ```bash
   # Correct format (double underscore for nested properties)
   export AzureAd__ClientId="value"
   export AzureAd__ClientSecret="value"
   export AzureAd__TenantId="value"
   
   # Not single underscore
   export AzureAd_ClientId="value"  # Wrong
   ```

2. **Configuration Precedence**:
   - Command line arguments (highest)
   - Environment variables
   - User secrets
   - appsettings.{Environment}.json
   - appsettings.json (lowest)

## Performance Monitoring

### Key Metrics to Monitor

1. **Response Times**:
   - Tool execution duration
   - Storage operation latency
   - SEC API response times

2. **Error Rates**:
   - Failed tool executions
   - Authentication failures
   - Storage connection errors

3. **Resource Usage**:
   - CPU utilization
   - Memory consumption
   - Network throughput
   - Disk I/O

4. **Business Metrics**:
   - Number of documents indexed
   - Search query frequency
   - Most popular companies/forms

### Application Insights Queries

```kusto
// Average response time by tool
requests
| where name startswith "POST /mcp/tools/"
| summarize avg(duration) by name
| order by avg_duration desc

// Error rate by endpoint
requests
| where resultCode >= 400
| summarize count() by name, resultCode
| order by count_ desc

// Memory usage over time
performanceCounters
| where counter == "% Processor Time"
| summarize avg(value) by bin(timestamp, 1h)
| render timechart
```

## Escalation Procedures

### When to Escalate

1. **Critical Issues**:
   - Service completely unavailable
   - Data corruption detected
   - Security breach suspected

2. **Performance Issues**:
   - Response times > 60 seconds consistently
   - Error rates > 5%
   - Memory usage > 90%

3. **Business Impact**:
   - Key customer workflows blocked
   - SLA violations
   - Revenue-impacting incidents

### Escalation Contact Information

1. **Development Team**: development@company.com
2. **Infrastructure Team**: infrastructure@company.com
3. **Security Team**: security@company.com
4. **Management**: management@company.com

### Information to Include

```markdown
## Incident Report

**Severity**: Critical/High/Medium/Low
**Time Started**: YYYY-MM-DD HH:MM UTC
**Environment**: Production/Staging/Development

**Summary**: Brief description of the issue

**Symptoms**:
- What users are experiencing
- Error messages observed
- Systems affected

**Diagnostic Information**:
- Server logs (last 30 minutes)
- Health check results
- Performance metrics
- Recent changes

**Attempted Solutions**:
- What has been tried
- Results of attempted fixes

**Business Impact**:
- Number of users affected
- Revenue impact
- SLA implications
```

## Prevention Best Practices

1. **Regular Health Checks**:
   - Automated monitoring
   - Proactive alerting
   - Performance baselines

2. **Configuration Management**:
   - Version controlled configurations
   - Environment-specific settings
   - Secret rotation procedures

3. **Capacity Planning**:
   - Resource usage trends
   - Growth projections
   - Scaling triggers

4. **Disaster Recovery**:
   - Regular backups
   - Recovery procedures
   - Business continuity plans

5. **Security Practices**:
   - Regular security assessments
   - Vulnerability management
   - Access control reviews