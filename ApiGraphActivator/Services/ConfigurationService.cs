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
            var config = new CrawlConfiguration
            {
                LastCrawlDate = DateTime.UtcNow,
                Companies = companies,
                TotalCompanies = companies.Count
            };

            var jsonString = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, jsonString);
            
            Console.WriteLine($"Saved {companies.Count} companies to config file: {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving crawled companies config: {ex.Message}");
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
}

public class CrawlConfiguration
{
    public DateTime LastCrawlDate { get; set; }
    public List<Company> Companies { get; set; } = new();
    public int TotalCompanies { get; set; }
}
