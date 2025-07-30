using System.Text.Json;

namespace ApiGraphActivator.Services;

public static class DataCollectionConfigurationService
{
    private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data-collection-config.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<DataCollectionConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Console.WriteLine("No data collection config file found. Creating default.");
                var defaultConfig = new DataCollectionConfiguration();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }

            var jsonString = await File.ReadAllTextAsync(ConfigFilePath);
            var config = JsonSerializer.Deserialize<DataCollectionConfiguration>(jsonString, JsonOptions);
            
            Console.WriteLine($"Loaded data collection config: {config?.YearsOfData} years of data");
            return config ?? new DataCollectionConfiguration();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data collection config: {ex.Message}");
            return new DataCollectionConfiguration();
        }
    }

    public static async Task SaveConfigurationAsync(DataCollectionConfiguration config)
    {
        try
        {
            config.LastUpdated = DateTime.UtcNow;
            var jsonString = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, jsonString);
            
            Console.WriteLine($"Saved data collection config: {config.YearsOfData} years of data");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving data collection config: {ex.Message}");
        }
    }

    public static async Task<int> GetYearsOfDataAsync()
    {
        var config = await LoadConfigurationAsync();
        return config.YearsOfData;
    }

    public static async Task SetYearsOfDataAsync(int years)
    {
        var config = await LoadConfigurationAsync();
        config.YearsOfData = Math.Max(1, Math.Min(years, 10)); // Limit between 1-10 years
        await SaveConfigurationAsync(config);
    }

    public static async Task<List<string>> GetIncludedFormTypesAsync()
    {
        var config = await LoadConfigurationAsync();
        return config.IncludedFormTypes;
    }
}

public class DataCollectionConfiguration
{
    public int YearsOfData { get; set; } = 3;
    public List<string> IncludedFormTypes { get; set; } = new() { "10-K", "10-Q", "8-K", "DEF 14A" };
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
