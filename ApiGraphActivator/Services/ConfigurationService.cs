using System.Text.Json;

namespace ApiGraphActivator.Services;

public static class ConfigurationService
{
    private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crawled-companies.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task SaveCrawledCompaniesAsync(List<Company> companies, string? connectionId = null)
    {
        try
        {
            // Use default connection if none specified
            if (string.IsNullOrEmpty(connectionId))
            {
                connectionId = ConnectionConfiguration.ExternalConnection.Id!;
            }

            // Load existing configuration
            var existingConfig = await LoadCrawledCompaniesConfigAsync();
            
            // Get or create connection-specific company list
            if (!existingConfig.ConnectionCompanies.ContainsKey(connectionId))
            {
                existingConfig.ConnectionCompanies[connectionId] = new List<Company>();
            }

            var existingCompanies = existingConfig.ConnectionCompanies[connectionId];
            
            // Merge new companies with existing ones (avoid duplicates based on CIK)
            var mergedCompanies = new List<Company>(existingCompanies);
            
            foreach (var newCompany in companies)
            {
                // Check if company already exists (by CIK)
                var existingCompany = mergedCompanies.FirstOrDefault(c => c.Cik == newCompany.Cik);
                if (existingCompany == null)
                {
                    // Add new company with current timestamp
                    newCompany.LastCrawledDate = DateTime.UtcNow;
                    mergedCompanies.Add(newCompany);
                    Console.WriteLine($"Added new company to connection {connectionId}: {newCompany.Ticker} - {newCompany.Title}");
                }
                else
                {
                    // Update existing company's timestamp only
                    existingCompany.LastCrawledDate = DateTime.UtcNow;
                    Console.WriteLine($"Updated crawl timestamp for existing company in connection {connectionId}: {existingCompany.Ticker} - {existingCompany.Title}");
                }
            }

            // Update the connection-specific data
            existingConfig.ConnectionCompanies[connectionId] = mergedCompanies;
            existingConfig.LastCrawlDate = DateTime.UtcNow;

            var jsonString = JsonSerializer.Serialize(existingConfig, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, jsonString);
            
            Console.WriteLine($"Saved {mergedCompanies.Count} total companies for connection {connectionId} (processed {companies.Count} in this crawl)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving crawled companies config: {ex.Message}");
        }
    }

    public static async Task UpdateCrawledCompanyTimestampsAsync(List<Company> processedCompanies, string? connectionId = null)
    {
        try
        {
            // Use default connection if none specified
            if (string.IsNullOrEmpty(connectionId))
            {
                connectionId = ConnectionConfiguration.ExternalConnection.Id!;
            }

            // Load existing configuration
            var existingConfig = await LoadCrawledCompaniesConfigAsync();
            if (!existingConfig.ConnectionCompanies.ContainsKey(connectionId) || 
                existingConfig.ConnectionCompanies[connectionId] == null)
            {
                Console.WriteLine($"No existing companies found for connection {connectionId} to update timestamps");
                return;
            }

            var connectionCompanies = existingConfig.ConnectionCompanies[connectionId];

            // Update timestamps only for companies that were actually processed
            bool updated = false;
            foreach (var processedCompany in processedCompanies)
            {
                var existingCompany = connectionCompanies.FirstOrDefault(c => c.Cik == processedCompany.Cik);
                if (existingCompany != null)
                {
                    existingCompany.LastCrawledDate = DateTime.UtcNow;
                    Console.WriteLine($"Updated timestamp for processed company in connection {connectionId}: {existingCompany.Ticker} - {existingCompany.Title}");
                    updated = true;
                }
            }

            if (updated)
            {
                // Save the updated configuration
                existingConfig.LastCrawlDate = DateTime.UtcNow;

                var jsonString = JsonSerializer.Serialize(existingConfig, JsonOptions);
                await File.WriteAllTextAsync(ConfigFilePath, jsonString);
                
                Console.WriteLine($"Updated timestamps for {processedCompanies.Count} processed companies in connection {connectionId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating company timestamps: {ex.Message}");
        }
    }

    private static async Task<CrawlConfigurationV2> LoadCrawledCompaniesConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Console.WriteLine("No crawled companies config file found. Creating new configuration.");
                return new CrawlConfigurationV2();
            }

            var jsonString = await File.ReadAllTextAsync(ConfigFilePath);
            
            if (string.IsNullOrEmpty(jsonString))
            {
                Console.WriteLine("JSON string is null or empty");
                return new CrawlConfigurationV2();
            }
            
            // Try to deserialize as new format first
            try
            {
                var configV2 = JsonSerializer.Deserialize<CrawlConfigurationV2>(jsonString, JsonOptions);
                if (configV2?.ConnectionCompanies != null)
                {
                    Console.WriteLine($"Loaded configuration with {configV2.ConnectionCompanies.Count} connections.");
                    return configV2;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize as V2 format: {ex.Message}");
                // Fall back to old format for migration
                Console.WriteLine("Attempting to migrate from old configuration format...");
            }

            // Try to deserialize as old format for migration
            var oldConfig = JsonSerializer.Deserialize<CrawlConfiguration>(jsonString, JsonOptions);
            if (oldConfig?.Companies != null)
            {
                // Migrate to new format
                var newConfig = new CrawlConfigurationV2
                {
                    LastCrawlDate = oldConfig.LastCrawlDate,
                    ConnectionCompanies = new Dictionary<string, List<Company>>
                    {
                        // Migrate old companies to default connection
                        { ConnectionConfiguration.ExternalConnection.Id!, oldConfig.Companies }
                    }
                };
                
                Console.WriteLine($"Migrated {oldConfig.Companies.Count} companies from old format to connection {ConnectionConfiguration.ExternalConnection.Id}");
                
                // Save migrated configuration
                var migratedJsonString = JsonSerializer.Serialize(newConfig, JsonOptions);
                await File.WriteAllTextAsync(ConfigFilePath, migratedJsonString);
                
                return newConfig;
            }

            Console.WriteLine("Could not parse configuration file. Creating new configuration.");
            return new CrawlConfigurationV2();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading crawled companies config: {ex.Message}");
            return new CrawlConfigurationV2();
        }
    }

    public static async Task<CrawlConfiguration?> LoadCrawledCompaniesAsync(string? connectionId = null)
    {
        try
        {
            // Use default connection if none specified
            if (string.IsNullOrEmpty(connectionId))
            {
                connectionId = ConnectionConfiguration.ExternalConnection.Id!;
            }

            var configV2 = await LoadCrawledCompaniesConfigAsync();
            
            if (configV2.ConnectionCompanies.ContainsKey(connectionId))
            {
                var companies = configV2.ConnectionCompanies[connectionId];
                Console.WriteLine($"Loaded {companies?.Count ?? 0} companies from connection {connectionId}.");
                
                return new CrawlConfiguration
                {
                    LastCrawlDate = configV2.LastCrawlDate,
                    Companies = companies ?? new List<Company>(),
                    TotalCompanies = companies?.Count ?? 0
                };
            }

            Console.WriteLine($"No companies found for connection {connectionId}.");
            return new CrawlConfiguration
            {
                LastCrawlDate = configV2.LastCrawlDate,
                Companies = new List<Company>(),
                TotalCompanies = 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading crawled companies config: {ex.Message}");
            return null;
        }
    }

    public static async Task<List<Company>> GetPreviouslyCrawledCompaniesAsync(string? connectionId = null)
    {
        var config = await LoadCrawledCompaniesAsync(connectionId);
        return config?.Companies ?? new List<Company>();
    }

    public static async Task<int> GetTotalCrawledCompaniesCountAsync(string? connectionId = null)
    {
        var config = await LoadCrawledCompaniesAsync(connectionId);
        return config?.TotalCompanies ?? 0;
    }

    public static async Task<DateTime?> GetLastCrawlDateAsync(string? connectionId = null)
    {
        var config = await LoadCrawledCompaniesAsync(connectionId);
        return config?.LastCrawlDate;
    }
}

public class CrawlConfiguration
{
    public DateTime LastCrawlDate { get; set; }
    public List<Company> Companies { get; set; } = new();
    public int TotalCompanies { get; set; }
}

public class CrawlConfigurationV2
{
    public DateTime LastCrawlDate { get; set; }
    public Dictionary<string, List<Company>> ConnectionCompanies { get; set; } = new();
}
