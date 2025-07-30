using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class StorageConfigurationService
{
    private readonly ILogger<StorageConfigurationService> _logger;
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private StorageConfiguration? _currentConfig;
    private static InMemoryStorageService? _singletonInMemoryService;

    public StorageConfigurationService(ILogger<StorageConfigurationService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage-config.json");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<StorageConfiguration> GetConfigurationAsync()
    {
        if (_currentConfig != null)
            return _currentConfig;

        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _currentConfig = JsonSerializer.Deserialize<StorageConfiguration>(json, _jsonOptions);
            }
            
            _currentConfig ??= new StorageConfiguration(); // Default to Local
            
            _logger.LogInformation("Loaded storage configuration: {Provider}", _currentConfig.Provider);
            return _currentConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load storage configuration, using defaults");
            _currentConfig = new StorageConfiguration();
            return _currentConfig;
        }
    }

    public async Task SaveConfigurationAsync(StorageConfiguration config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
            _currentConfig = config;
            
            _logger.LogInformation("Saved storage configuration: {Provider}", config.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save storage configuration");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(StorageConfiguration config)
    {
        try
        {
            var storageService = CreateStorageService(config);
            await storageService.InitializeAsync();
            return await storageService.IsHealthyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for {Provider}", config.Provider);
            return false;
        }
    }

    public ICrawlStorageService CreateStorageService(StorageConfiguration? config = null)
    {
        config ??= _currentConfig ?? new StorageConfiguration();

        return config.Provider.ToLower() switch
        {
            "azure" => new AzureStorageService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AzureStorageService>(),
                config),
            "local" => new LocalFileStorageService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LocalFileStorageService>(),
                config),
            "memory" => _singletonInMemoryService ??= new InMemoryStorageService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<InMemoryStorageService>()),
            _ => new LocalFileStorageService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LocalFileStorageService>(),
                config)
        };
    }

    public async Task<ICrawlStorageService> GetStorageServiceAsync()
    {
        var config = await GetConfigurationAsync();
        return CreateStorageService(config);
    }

    public async Task<OverallCrawlMetrics> GetOverallMetricsAsync()
    {
        try
        {
            var storageService = await GetStorageServiceAsync();
            await storageService.InitializeAsync();
            
            // Get metrics for all companies
            var overallMetrics = await storageService.GetCrawlMetricsAsync();
            
            // Get individual company metrics to build comprehensive overview
            var crawledCompanies = await ConfigurationService.LoadCrawledCompaniesAsync();
            var companyMetrics = new List<CrawlMetrics>();
            
            if (crawledCompanies?.Companies != null)
            {
                foreach (var company in crawledCompanies.Companies)
                {
                    try
                    {
                        var companyMetric = await storageService.GetCrawlMetricsAsync(company.Title);
                        if (companyMetric.TotalDocuments > 0)
                        {
                            companyMetrics.Add(companyMetric);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get metrics for company: {Company}", company.Title);
                    }
                }
            }

            return new OverallCrawlMetrics
            {
                TotalCompanies = companyMetrics.Count,
                TotalDocuments = overallMetrics.TotalDocuments,
                ProcessedDocuments = overallMetrics.ProcessedDocuments,
                SuccessfulDocuments = overallMetrics.SuccessfulDocuments,
                FailedDocuments = overallMetrics.FailedDocuments,
                LastCrawlDate = crawledCompanies?.LastCrawlDate,
                CompanyMetrics = companyMetrics,
                FormTypeCounts = overallMetrics.FormTypeCounts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get overall crawl metrics");
            throw;
        }
    }
}
