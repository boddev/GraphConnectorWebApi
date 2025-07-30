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

    public static async Task SaveCrawledCompaniesAsync(List<Company> companies)
    {
        try
        {
            // Load existing companies first
            var existingConfig = await LoadCrawledCompaniesAsync();
            var existingCompanies = existingConfig?.Companies ?? new List<Company>();
            
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
                    Console.WriteLine($"Added new company: {newCompany.Ticker} - {newCompany.Title}");
                }
                else
                {
                    // Update existing company's timestamp only
                    existingCompany.LastCrawledDate = DateTime.UtcNow;
                    Console.WriteLine($"Updated crawl timestamp for existing company: {existingCompany.Ticker} - {existingCompany.Title}");
                }
            }

            var config = new CrawlConfiguration
            {
                LastCrawlDate = DateTime.UtcNow,
                Companies = mergedCompanies,
                TotalCompanies = mergedCompanies.Count
            };

            var jsonString = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, jsonString);
            
            Console.WriteLine($"Saved {mergedCompanies.Count} total companies to config file (processed {companies.Count} in this crawl): {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving crawled companies config: {ex.Message}");
        }
    }

    public static async Task UpdateCrawledCompanyTimestampsAsync(List<Company> processedCompanies)
    {
        try
        {
            // Load existing configuration
            var existingConfig = await LoadCrawledCompaniesAsync();
            if (existingConfig?.Companies == null)
            {
                Console.WriteLine("No existing companies found to update timestamps");
                return;
            }

            // Update timestamps only for companies that were actually processed
            bool updated = false;
            foreach (var processedCompany in processedCompanies)
            {
                var existingCompany = existingConfig.Companies.FirstOrDefault(c => c.Cik == processedCompany.Cik);
                if (existingCompany != null)
                {
                    existingCompany.LastCrawledDate = DateTime.UtcNow;
                    Console.WriteLine($"Updated timestamp for processed company: {existingCompany.Ticker} - {existingCompany.Title}");
                    updated = true;
                }
            }

            if (updated)
            {
                // Save the updated configuration
                var config = new CrawlConfiguration
                {
                    LastCrawlDate = DateTime.UtcNow,
                    Companies = existingConfig.Companies,
                    TotalCompanies = existingConfig.Companies.Count
                };

                var jsonString = JsonSerializer.Serialize(config, JsonOptions);
                await File.WriteAllTextAsync(ConfigFilePath, jsonString);
                
                Console.WriteLine($"Updated timestamps for {processedCompanies.Count} processed companies");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating company timestamps: {ex.Message}");
        }
    }

    public static async Task<CrawlConfiguration?> LoadCrawledCompaniesAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Console.WriteLine("No crawled companies config file found.");
                return null;
            }

            var jsonString = await File.ReadAllTextAsync(ConfigFilePath);
            var config = JsonSerializer.Deserialize<CrawlConfiguration>(jsonString, JsonOptions);
            
            Console.WriteLine($"Loaded {config?.Companies?.Count ?? 0} companies from config file.");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading crawled companies config: {ex.Message}");
            return null;
        }
    }

    public static async Task<List<Company>> GetPreviouslyCrawledCompaniesAsync()
    {
        var config = await LoadCrawledCompaniesAsync();
        return config?.Companies ?? new List<Company>();
    }

    public static async Task<int> GetTotalCrawledCompaniesCountAsync()
    {
        var config = await LoadCrawledCompaniesAsync();
        return config?.TotalCompanies ?? 0;
    }

    public static async Task<DateTime?> GetLastCrawlDateAsync()
    {
        var config = await LoadCrawledCompaniesAsync();
        return config?.LastCrawlDate;
    }
}

public class CrawlConfiguration
{
    public DateTime LastCrawlDate { get; set; }
    public List<Company> Companies { get; set; } = new();
    public int TotalCompanies { get; set; }
}
