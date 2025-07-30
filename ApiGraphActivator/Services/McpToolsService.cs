using ApiGraphActivator.Models.Mcp;
using System.Text.Json;

namespace ApiGraphActivator.Services;

/// <summary>
/// Sample service demonstrating MCP tools using existing API functionality
/// </summary>
public class McpToolsService
{
    private readonly ILogger<McpToolsService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public McpToolsService(ILogger<McpToolsService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get SEC company information by ticker or name
    /// </summary>
    [McpTool(
        Name = "get_company_info",
        Description = "Retrieve SEC company information including CIK, ticker symbol, and official company name from the SEC database",
        Category = "Edgar Data"
    )]
    public async Task<object> GetCompanyInfo(
        [McpToolParameter(Description = "Company ticker symbol (e.g., AAPL) or partial company name", Required = true)]
        string query)
    {
        try
        {
            _logger.LogInformation("Getting company info for query: {Query}", query);
            
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
            
            var response = await httpClient.GetStringAsync("https://www.sec.gov/files/company_tickers.json");
            var companies = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);
            
            var matchingCompanies = new List<object>();
            
            foreach (var company in companies?.Values ?? Enumerable.Empty<JsonElement>())
            {
                var ticker = company.GetProperty("ticker").GetString() ?? "";
                var title = company.GetProperty("title").GetString() ?? "";
                var cik = company.GetProperty("cik_str").GetInt32();
                
                if (ticker.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matchingCompanies.Add(new
                    {
                        ticker,
                        title,
                        cik = cik.ToString("D10")
                    });
                }
                
                if (matchingCompanies.Count >= 10) break; // Limit results
            }
            
            return new
            {
                query,
                matchCount = matchingCompanies.Count,
                companies = matchingCompanies
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company info for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Get storage configuration status
    /// </summary>
    [McpTool(
        Name = "get_storage_config",
        Description = "Retrieve current storage configuration and health status for the Graph Connector",
        Category = "Configuration"
    )]
    public async Task<object> GetStorageConfiguration()
    {
        try
        {
            _logger.LogInformation("Getting storage configuration");
            
            var storageConfigService = _serviceProvider.GetRequiredService<StorageConfigurationService>();
            var config = await storageConfigService.GetConfigurationAsync();
            
            return new
            {
                storageType = config.Provider,
                isConfigured = !string.IsNullOrEmpty(config.AzureConnectionString),
                hasTableName = !string.IsNullOrEmpty(config.CompanyTableName),
                hasBlobContainer = !string.IsNullOrEmpty(config.BlobContainerName),
                isHealthy = await storageConfigService.TestConnectionAsync(config)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage configuration");
            throw;
        }
    }

    /// <summary>
    /// Get overall crawl metrics and status
    /// </summary>
    [McpTool(
        Name = "get_crawl_status",
        Description = "Get comprehensive crawl status including processed documents count, success rate, and health indicators",
        Category = "Monitoring"
    )]
    public async Task<object> GetCrawlStatus()
    {
        try
        {
            _logger.LogInformation("Getting crawl status");
            
            var storageConfigService = _serviceProvider.GetRequiredService<StorageConfigurationService>();
            var storageService = await storageConfigService.GetStorageServiceAsync();
            await storageService.InitializeAsync();
            
            var overallMetrics = await storageService.GetCrawlMetricsAsync();
            var unprocessedDocs = await storageService.GetUnprocessedAsync();
            
            return new
            {
                totalDocuments = overallMetrics.TotalDocuments,
                processedDocuments = overallMetrics.ProcessedDocuments,
                successfulDocuments = overallMetrics.SuccessfulDocuments,
                failedDocuments = overallMetrics.FailedDocuments,
                pendingDocuments = unprocessedDocs.Count,
                successRate = overallMetrics.SuccessRate,
                lastProcessedDate = overallMetrics.LastProcessedDate,
                storageType = storageService.GetStorageType(),
                isHealthy = await storageService.IsHealthyAsync()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting crawl status");
            throw;
        }
    }

    /// <summary>
    /// Get metrics for a specific company
    /// </summary>
    [McpTool(
        Name = "get_company_metrics",
        Description = "Get detailed crawl metrics for a specific company including document counts and processing status",
        Category = "Monitoring"
    )]
    public async Task<object> GetCompanyMetrics(
        [McpToolParameter(Description = "Company name to get metrics for", Required = true)]
        string companyName)
    {
        try
        {
            _logger.LogInformation("Getting company metrics for: {CompanyName}", companyName);
            
            var storageConfigService = _serviceProvider.GetRequiredService<StorageConfigurationService>();
            var storageService = await storageConfigService.GetStorageServiceAsync();
            await storageService.InitializeAsync();
            
            var companyMetrics = await storageService.GetCrawlMetricsAsync(companyName);
            
            return new
            {
                companyName,
                totalDocuments = companyMetrics.TotalDocuments,
                processedDocuments = companyMetrics.ProcessedDocuments,
                successfulDocuments = companyMetrics.SuccessfulDocuments,
                failedDocuments = companyMetrics.FailedDocuments,
                successRate = companyMetrics.SuccessRate,
                lastProcessedDate = companyMetrics.LastProcessedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company metrics for: {CompanyName}", companyName);
            throw;
        }
    }

    /// <summary>
    /// Start a content crawl for specific companies
    /// </summary>
    [McpTool(
        Name = "start_crawl",
        Description = "Initiate a background crawl process for specified companies to extract and index SEC filings",
        Category = "Operations"
    )]
    public async Task<object> StartCrawl(
        [McpToolParameter(Description = "Array of companies to crawl, each with cik, ticker, and title", Required = true)]
        object companiesData)
    {
        try
        {
            _logger.LogInformation("Starting crawl with company data");
            
            var taskQueue = _serviceProvider.GetRequiredService<BackgroundTaskQueue>();
            
            // Parse companies data
            var companies = new List<Company>();
            if (companiesData is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    var company = new Company
                    {
                        Cik = item.GetProperty("cik").GetInt32(),
                        Ticker = item.GetProperty("ticker").GetString() ?? "",
                        Title = item.GetProperty("title").GetString() ?? ""
                    };
                    companies.Add(company);
                }
            }
            
            if (!companies.Any())
            {
                return new { error = "No valid companies provided" };
            }
            
            // Save companies to config file for persistence
            await ConfigurationService.SaveCrawledCompaniesAsync(companies);
            
            // Queue the background task
            await taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                _logger.LogInformation("Background crawl task started for {CompanyCount} companies", companies.Count);
                await ContentService.LoadContentForCompanies(companies);
                _logger.LogInformation("Background crawl task completed for {CompanyCount} companies", companies.Count);
            });
            
            return new
            {
                status = "started",
                message = $"Crawl started successfully for {companies.Count} companies",
                companyCount = companies.Count,
                companies = companies.Select(c => new { c.Ticker, c.Title }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting crawl");
            throw;
        }
    }

    /// <summary>
    /// Get previously crawled companies information
    /// </summary>
    [McpTool(
        Name = "get_crawled_companies",
        Description = "Retrieve information about companies that have been previously crawled, including last crawl date",
        Category = "Edgar Data"
    )]
    public async Task<object> GetCrawledCompanies()
    {
        try
        {
            _logger.LogInformation("Getting previously crawled companies");
            
            var config = await ConfigurationService.LoadCrawledCompaniesAsync();
            
            if (config == null)
            {
                return new
                {
                    lastCrawlDate = (DateTime?)null,
                    companies = new List<Company>(),
                    totalCompanies = 0
                };
            }
            
            return new
            {
                lastCrawlDate = config.LastCrawlDate,
                companies = config.Companies,
                totalCompanies = config.TotalCompanies
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting crawled companies");
            throw;
        }
    }
}