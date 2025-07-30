# Frequently Asked Questions (FAQ)

Common questions and answers about the SEC Edgar MCP server.

## General Questions

### Q: What is the MCP server and how does it differ from the Graph Connector?

**A:** The MCP (Model Context Protocol) server provides structured tool-based access to SEC filing documents, designed for integration with AI agents and automation systems. The Graph Connector indexes documents into Microsoft 365 for search and Copilot. They can work together or independently:

- **MCP Server**: Direct API access to SEC documents via standardized tools
- **Graph Connector**: Documents indexed into Microsoft 365 search and Copilot
- **Combined**: Best of both worlds - direct API access and Microsoft 365 integration

### Q: Do I need Azure to run the MCP server?

**A:** No, Azure is optional:

**Required for Graph Connector functionality**:
- Azure AD app registration (for Microsoft Graph permissions)

**Optional Azure services**:
- Azure Table Storage (can use in-memory or local file storage instead)
- Application Insights (can use local logging instead)
- Azure OpenAI (optional content enhancement)

**Local alternatives**:
- In-memory storage for development
- Local file storage for production
- Local logging and monitoring

### Q: What SEC form types are supported?

**A:** The MCP server supports these SEC form types:
- **10-K**: Annual reports providing comprehensive company overviews
- **10-Q**: Quarterly reports with interim financial statements  
- **8-K**: Current reports for significant company events
- **10-K/A**: Amended annual reports
- **10-Q/A**: Amended quarterly reports
- **8-K/A**: Amended current reports

Form types are case-sensitive and must use the exact format shown above.

### Q: How current is the SEC data?

**A:** The MCP server accesses real-time data from the SEC EDGAR database. Documents are available as soon as they're published by the SEC, typically within minutes of filing. The server includes built-in rate limiting to respect SEC API guidelines.

## Technical Questions

### Q: What are the system requirements?

**A:** Minimum requirements:
- **.NET 8 SDK/Runtime**
- **2GB RAM** (4GB+ recommended for production)
- **10GB+ disk space** (for caching and storage)
- **Internet connection** (for SEC API access)
- **Windows, Linux, or macOS**

Production recommendations:
- **8GB+ RAM**
- **50GB+ SSD storage**
- **Load balancer** for high availability
- **Monitoring solution** (Application Insights recommended)

### Q: How does authentication work?

**A:** The MCP server uses Azure AD app registration for Microsoft Graph permissions:

1. **Setup**: Create Azure AD app registration with required permissions
2. **Configuration**: Set client ID, secret, and tenant ID in server configuration
3. **Admin Consent**: One-time admin consent grant for Graph permissions
4. **Operation**: Server uses app registration credentials for all Graph operations

The MCP tools themselves don't require additional authentication beyond the server's Graph permissions.

### Q: Can I run multiple instances for high availability?

**A:** Yes, the MCP server supports horizontal scaling:

**Stateless Design**: Each server instance is stateless and can handle any request

**Shared Storage**: Use Azure Table Storage or shared file system for data consistency

**Load Balancing**: Any HTTP load balancer can distribute requests across instances

**Session Management**: No session state to manage between instances

Example setup:
```
Load Balancer → Instance 1 ↘
              → Instance 2 → Shared Azure Storage
              → Instance 3 ↗
```

### Q: What's the difference between storage backends?

**A:** Three storage options available:

| Feature | In-Memory | Local File | Azure Table |
|---------|-----------|------------|-------------|
| **Persistence** | No | Yes | Yes |
| **Scalability** | Single instance | Single instance | Multi-instance |
| **Performance** | Fastest | Fast | Network dependent |
| **Backup** | Not applicable | Manual | Automatic |
| **Cost** | Free | Storage cost only | Azure service costs |
| **Use Case** | Development/testing | Single-node production | Cloud production |

### Q: How do I migrate between storage backends?

**A:** Migration process depends on source and target:

**From In-Memory**: No data to migrate (start fresh with new backend)

**From Local File to Azure**:
```bash
# Export data from local files
cp -r /data/sec-filings /backup/

# Configure Azure Storage
export STORAGE_TYPE="AzureTable"
export TableStorage="your-azure-connection-string"

# Restart server (will initialize new storage)
systemctl restart sec-edgar-mcp

# Re-run initial data load if needed
curl -X POST http://localhost:5236/loadcontent \
  -H "Content-Type: application/json" \
  -d '"your-tenant-id"'
```

**From Azure to Local File**:
```bash
# Export Azure tables
az storage table export --account-name youraccount --table-name trackedCompanies

# Configure local storage
export STORAGE_TYPE="LocalFile"
export STORAGE_PATH="/data/sec-filings"

# Create storage directory
mkdir -p /data/sec-filings

# Restart and reload data
systemctl restart sec-edgar-mcp
```

## Integration Questions

### Q: How do I integrate with my existing application?

**A:** Multiple integration approaches:

**1. Direct HTTP API**:
```bash
curl -X POST http://localhost:5236/mcp/tools/company-search \
  -H "Content-Type: application/json" \
  -d '{"companyName": "Apple Inc.", "formTypes": ["10-K"]}'
```

**2. Language-specific clients**:
- [Python client examples](../integration/python-examples.md)
- [Node.js client examples](../integration/nodejs-examples.md)

**3. OpenAPI specification**:
- Use the [OpenAPI spec](../api/mcp-tools-openapi.yaml) to generate clients
- Compatible with Swagger Codegen, OpenAPI Generator

**4. Webhook/Event integration**:
- Poll for new documents using date filters
- Implement change detection logic

### Q: Can I use this with Microsoft Copilot?

**A:** Yes, multiple integration paths:

**1. Direct MCP Tools**: Use MCP tools directly in Copilot plugins/skills

**2. Graph Connector Integration**: Documents indexed via Graph Connector are automatically available in Copilot

**3. Custom Copilot Skills**: Build custom skills that call MCP tools

**4. Power Platform**: Use Power Automate flows to call MCP tools

See the [Copilot Integration Guide](../integration/copilot-integration.md) for detailed examples.

### Q: How do I handle rate limiting?

**A:** Built-in rate limiting for SEC API:

**Automatic Handling**: Server automatically respects SEC rate limits with exponential backoff

**Best Practices**:
- Use date ranges to limit search scope
- Implement pagination for large result sets
- Cache frequently accessed data
- Use `includeContent: false` when full content isn't needed

**Monitoring**: Check logs for rate limit messages:
```bash
grep "rate.limit\|429" /var/log/sec-edgar-mcp/application.log
```

### Q: What's the maximum number of results I can get?

**A:** Limits depend on the tool:

| Tool | Max Page Size | Recommended Page Size |
|------|---------------|----------------------|
| Company Search | 1,000 | 50-100 |
| Form Filter | 1,000 | 50-100 |
| Content Search | 100 | 10-50 |

**Getting More Results**: Use pagination to access all results:
```javascript
let page = 1;
let allResults = [];

while (true) {
  const response = await client.searchByCompany({
    companyName: "Apple",
    page: page,
    pageSize: 100
  });
  
  allResults.push(...response.content.items);
  
  if (!response.content.hasNextPage) break;
  page++;
}
```

## Performance Questions

### Q: How fast are the search operations?

**A:** Typical performance characteristics:

| Operation | Response Time | Notes |
|-----------|---------------|-------|
| Tool Discovery | <100ms | Cached after first call |
| Company Search | 200ms-2s | Depends on result count |
| Form Filter | 500ms-5s | Depends on date range |
| Content Search | 1s-10s | Depends on content size |

**Factors affecting performance**:
- Storage backend (Azure vs Local vs In-Memory)
- Network latency to SEC servers
- Search complexity and result size
- Server resources (CPU/RAM)

### Q: How much storage space do I need?

**A:** Storage requirements vary by usage:

**Metadata Only** (no full content):
- ~1KB per document record
- 10,000 documents ≈ 10MB

**With Full Content**:
- ~100KB average per document
- 10,000 documents ≈ 1GB

**Caching Considerations**:
- In-memory cache: 512MB-2GB RAM
- Local file cache: 10GB-100GB disk
- Azure blob storage: Pay per GB used

**Estimation Formula**:
```
Storage Needed = (Number of Companies × Average Documents per Company × Average Document Size)
```

Example for 100 companies with 2 years of filings:
```
100 companies × 50 documents × 100KB = 500MB
```

### Q: Can I improve search performance?

**A:** Several optimization strategies:

**1. Use Specific Date Ranges**:
```json
{
  "startDate": "2023-01-01",
  "endDate": "2023-12-31"
}
```

**2. Limit Result Sets**:
```json
{
  "pageSize": 50,
  "includeContent": false
}
```

**3. Cache Frequently Accessed Data**:
- Implement application-level caching
- Use CDN for static content
- Store popular search results

**4. Optimize Storage Backend**:
- Use Azure Storage in same region
- Configure appropriate connection pools
- Consider read replicas for heavy read workloads

**5. Scale Horizontally**:
- Run multiple server instances
- Use load balancer
- Distribute workload

## Business Questions

### Q: What are the costs involved?

**A:** Cost components:

**Free Components**:
- MCP server software (open source)
- SEC EDGAR data access
- In-memory storage option

**Optional Azure Costs**:
- Azure Storage: ~$0.01-0.05 per GB/month
- Application Insights: ~$2.30 per GB ingested
- Azure AD: Free tier usually sufficient

**Infrastructure Costs**:
- Server hosting (cloud or on-premise)
- Network bandwidth
- Backup storage

**Development/Maintenance**:
- Initial setup and integration
- Ongoing maintenance and updates
- Monitoring and support

### Q: Is this suitable for production use?

**A:** Yes, with proper configuration:

**Production-Ready Features**:
- Horizontal scaling support
- Multiple storage backend options
- Comprehensive error handling
- Built-in rate limiting
- Health check endpoints
- Structured logging

**Production Recommendations**:
- Use Azure Table Storage or equivalent
- Set up monitoring and alerting
- Implement proper backup procedures
- Configure HTTPS with valid certificates
- Use environment-specific configurations
- Set up CI/CD pipeline

### Q: What compliance considerations are there?

**A:** Important compliance aspects:

**Data Handling**:
- SEC data is public information
- No personal/private data stored
- Standard data retention policies apply

**Security**:
- HTTPS encryption in transit
- Azure Storage encryption at rest
- Secure credential management
- Regular security updates

**Audit Requirements**:
- Application Insights provides audit trail
- Request/response logging available
- User activity tracking possible

**Regulatory**:
- SEC rate limiting compliance built-in
- Proper User-Agent identification
- Terms of service compliance

## Support Questions

### Q: Where can I get help?

**A:** Support resources:

**Documentation**:
- [API Reference](../api/tools-reference.md)
- [Integration Guides](../integration/client-integration.md)
- [Troubleshooting Guide](../troubleshooting/common-issues.md)

**Community Support**:
- GitHub Issues for bug reports
- GitHub Discussions for questions
- Stack Overflow (tag: sec-edgar-mcp)

**Professional Support**:
- Contact development team for commercial support
- Professional services available for custom integrations

### Q: How do I report bugs or request features?

**A:** Use GitHub repository:

**Bug Reports**:
1. Check existing issues first
2. Create new issue with bug template
3. Include logs, configuration, and reproduction steps

**Feature Requests**:
1. Search existing feature requests
2. Create enhancement issue with feature template
3. Describe use case and expected behavior

**Security Issues**:
- Email security@company.com directly
- Do not create public GitHub issues for security vulnerabilities

### Q: How often is the software updated?

**A:** Release schedule:

**Regular Updates**:
- Bug fixes: As needed
- Feature releases: Monthly
- Security updates: Immediate

**Version Support**:
- Latest version: Full support
- Previous version: Security updates only
- Older versions: Community support only

**Upgrade Process**:
- Backwards compatible within major versions
- Migration guides provided for breaking changes
- Rolling upgrades supported

### Q: Can I contribute to the project?

**A:** Yes! Contributions welcome:

**Code Contributions**:
- Fork the repository
- Create feature branch
- Submit pull request with tests
- Follow coding standards

**Documentation**:
- Improve existing documentation
- Add new examples and tutorials
- Translate documentation

**Testing**:
- Report bugs and issues
- Test new features
- Provide feedback on usability

**Community**:
- Answer questions in discussions
- Help other users troubleshoot
- Share integration examples