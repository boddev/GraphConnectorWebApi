using System.Text.Json;

namespace ApiGraphActivator.Services;

public class ScheduleConfig
{
    public bool Enabled { get; set; } = false;
    public string Frequency { get; set; } = "Weekly"; // Daily, Weekly, Monthly
    public int Hour { get; set; } = 9; // 0-23, time of day to run
    public int DayOfWeek { get; set; } = 1; // For weekly: 0=Sunday, 1=Monday, etc.
    public int DayOfMonth { get; set; } = 1; // For monthly: 1-31
    public DateTime? LastScheduledRun { get; set; }
    public DateTime? NextScheduledRun { get; set; }
}

public class SchedulerService : BackgroundService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly BackgroundTaskQueue _taskQueue;
    private readonly string _configFilePath;

    public SchedulerService(ILogger<SchedulerService> logger, BackgroundTaskQueue taskQueue)
    {
        _logger = logger;
        _taskQueue = taskQueue;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduler-config.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExecuteScheduledTasks();
                
                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduler service");
                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Scheduler Service stopped");
    }

    private async Task CheckAndExecuteScheduledTasks()
    {
        var config = await LoadScheduleConfigAsync();
        
        if (!config.Enabled)
        {
            return;
        }

        var now = DateTime.Now;
        
        // Calculate next run time if not set
        if (config.NextScheduledRun == null)
        {
            config.NextScheduledRun = CalculateNextRunTime(config, now);
            await SaveScheduleConfigAsync(config);
            return;
        }

        // Check if it's time to run
        if (now >= config.NextScheduledRun)
        {
            _logger.LogInformation("🕐 SCHEDULED RECRAWL STARTING at {Time}", now);
            
            try
            {
                // Execute the recrawl
                await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
                {
                    _logger.LogInformation("🔄 Scheduled recrawl task started - processing previously crawled companies");
                    
                    // Load previously crawled companies
                    var crawledConfig = await ConfigurationService.LoadCrawledCompaniesAsync();
                    
                    if (crawledConfig?.Companies?.Any() == true)
                    {
                        _logger.LogInformation("📊 Starting scheduled recrawl for {CompanyCount} companies: {Companies}", 
                            crawledConfig.Companies.Count,
                            string.Join(", ", crawledConfig.Companies.Select(c => c.Ticker)));
                        
                        await ContentService.LoadContentForCompanies(crawledConfig.Companies);
                        
                        _logger.LogInformation("✅ Scheduled recrawl completed successfully for {CompanyCount} companies. Metrics have been updated.", 
                            crawledConfig.Companies.Count);
                    }
                    else
                    {
                        _logger.LogWarning("❌ No previously crawled companies found for scheduled recrawl");
                    }
                });

                // Update last run time and calculate next run
                config.LastScheduledRun = now;
                config.NextScheduledRun = CalculateNextRunTime(config, now);
                await SaveScheduleConfigAsync(config);
                
                _logger.LogInformation("📅 Next scheduled recrawl: {NextRun}", config.NextScheduledRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error executing scheduled recrawl");
            }
        }
    }

    private DateTime CalculateNextRunTime(ScheduleConfig config, DateTime fromTime)
    {
        var nextRun = fromTime.Date.AddHours(config.Hour);
        
        // If the time has already passed today, start from tomorrow
        if (nextRun <= fromTime)
        {
            nextRun = nextRun.AddDays(1);
        }

        switch (config.Frequency.ToLower())
        {
            case "daily":
                // Next run is tomorrow at the specified hour
                break;
                
            case "weekly":
                // Find next occurrence of the specified day of week
                while ((int)nextRun.DayOfWeek != config.DayOfWeek)
                {
                    nextRun = nextRun.AddDays(1);
                }
                break;
                
            case "monthly":
                // Find next occurrence of the specified day of month
                var targetDay = Math.Min(config.DayOfMonth, DateTime.DaysInMonth(nextRun.Year, nextRun.Month));
                
                if (nextRun.Day <= targetDay && nextRun > fromTime)
                {
                    // This month
                    nextRun = new DateTime(nextRun.Year, nextRun.Month, targetDay, config.Hour, 0, 0);
                }
                else
                {
                    // Next month
                    var nextMonth = nextRun.AddMonths(1);
                    targetDay = Math.Min(config.DayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    nextRun = new DateTime(nextMonth.Year, nextMonth.Month, targetDay, config.Hour, 0, 0);
                }
                break;
                
            default:
                throw new ArgumentException($"Invalid frequency: {config.Frequency}");
        }

        return nextRun;
    }

    public async Task<ScheduleConfig> LoadScheduleConfigAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return new ScheduleConfig();
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            return JsonSerializer.Deserialize<ScheduleConfig>(json) ?? new ScheduleConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading schedule config");
            return new ScheduleConfig();
        }
    }

    public async Task SaveScheduleConfigAsync(ScheduleConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogDebug("Schedule config saved to {FilePath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving schedule config");
            throw;
        }
    }
}
