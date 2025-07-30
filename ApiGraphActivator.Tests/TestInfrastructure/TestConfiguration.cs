using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.McpTools;
using ApiGraphActivator.Tests.TestData;

namespace ApiGraphActivator.Tests.TestInfrastructure;

/// <summary>
/// Test configuration and dependency injection setup
/// </summary>
public class TestServiceProvider
{
    private readonly ServiceProvider _serviceProvider;

    public TestServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configure logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Register mock services
        services.AddSingleton<ICrawlStorageService, MockCrawlStorageService>();
        services.AddSingleton<IGraphService, MockGraphService>();
        
        // Register real services that depend on mocks
        services.AddSingleton<DocumentSearchService>();
        
        // Register MCP tools
        services.AddSingleton<CompanySearchTool>();
        services.AddSingleton<FormFilterTool>();
        services.AddSingleton<ContentSearchTool>();
    }

    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}

/// <summary>
/// Base class for integration tests with common setup
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly TestServiceProvider ServiceProvider;
    protected readonly MockCrawlStorageService MockStorageService;
    protected readonly MockGraphService MockGraphService;

    protected IntegrationTestBase()
    {
        ServiceProvider = new TestServiceProvider();
        MockStorageService = (MockCrawlStorageService)ServiceProvider.GetService<ICrawlStorageService>();
        MockGraphService = (MockGraphService)ServiceProvider.GetService<IGraphService>();
    }

    protected virtual void SetupTestData()
    {
        // Override in derived classes to set up specific test data
    }

    public virtual void Dispose()
    {
        MockStorageService.ClearTestData();
        MockGraphService.ClearMockData();
        ServiceProvider.Dispose();
    }
}

/// <summary>
/// Test data management utilities
/// </summary>
public static class TestDataManager
{
    /// <summary>
    /// Creates a comprehensive test dataset for integration testing
    /// </summary>
    public static void SetupComprehensiveTestData(MockCrawlStorageService storageService)
    {
        var companies = new[]
        {
            "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc.", 
            "Alphabet Inc.", "Tesla Inc.", "Meta Platforms Inc."
        };

        var forms = new[] { "10-K", "10-Q", "8-K", "10-K/A", "10-Q/A", "8-K/A" };
        var random = new Random(42); // Fixed seed for reproducible tests

        foreach (var company in companies)
        {
            foreach (var form in forms)
            {
                // Create 2-5 documents per company/form combination
                var docCount = random.Next(2, 6);
                for (int i = 0; i < docCount; i++)
                {
                    var filingDate = DateTime.Now.AddDays(-random.Next(30, 1095)); // 1 month to 3 years ago
                    var document = TestDataBuilder.CreateDocumentInfo(
                        companyName: company,
                        form: form,
                        filingDate: filingDate,
                        processed: random.NextDouble() > 0.1); // 90% processed
                    
                    storageService.AddTestDocument(document);
                }
            }
        }
    }

    /// <summary>
    /// Creates test data focused on performance testing
    /// </summary>
    public static void SetupPerformanceTestData(MockCrawlStorageService storageService, int documentCount = 10000)
    {
        var companies = Enumerable.Range(1, 1000).Select(i => $"Company {i:D4}").ToArray();
        var forms = new[] { "10-K", "10-Q", "8-K" };
        var random = new Random(42);

        for (int i = 0; i < documentCount; i++)
        {
            var company = companies[i % companies.Length];
            var form = forms[i % forms.Length];
            var filingDate = DateTime.Now.AddDays(-random.Next(30, 2190)); // Up to 6 years ago

            var document = TestDataBuilder.CreateDocumentInfo(
                companyName: company,
                form: form,
                filingDate: filingDate,
                processed: true);

            storageService.AddTestDocument(document);
        }
    }

    /// <summary>
    /// Creates focused test data for specific scenarios
    /// </summary>
    public static void SetupScenarioTestData(MockCrawlStorageService storageService, string scenario)
    {
        switch (scenario.ToLowerInvariant())
        {
            case "apple_only":
                SetupAppleOnlyTestData(storageService);
                break;
            case "quarterly_reports":
                SetupQuarterlyReportsTestData(storageService);
                break;
            case "recent_filings":
                SetupRecentFilingsTestData(storageService);
                break;
            default:
                throw new ArgumentException($"Unknown test scenario: {scenario}");
        }
    }

    private static void SetupAppleOnlyTestData(MockCrawlStorageService storageService)
    {
        var forms = new[] { "10-K", "10-Q", "8-K" };
        
        foreach (var form in forms)
        {
            for (int year = 2020; year <= 2023; year++)
            {
                for (int quarter = 1; quarter <= (form == "10-K" ? 1 : 4); quarter++)
                {
                    var filingDate = new DateTime(year, quarter * 3, 15);
                    var document = TestDataBuilder.CreateDocumentInfo(
                        companyName: "Apple Inc.",
                        form: form,
                        filingDate: filingDate,
                        processed: true);
                    
                    storageService.AddTestDocument(document);
                }
            }
        }
    }

    private static void SetupQuarterlyReportsTestData(MockCrawlStorageService storageService)
    {
        var companies = new[] { "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc." };
        
        foreach (var company in companies)
        {
            for (int year = 2022; year <= 2023; year++)
            {
                for (int quarter = 1; quarter <= 4; quarter++)
                {
                    var filingDate = new DateTime(year, quarter * 3, 15);
                    var document = TestDataBuilder.CreateDocumentInfo(
                        companyName: company,
                        form: "10-Q",
                        filingDate: filingDate,
                        processed: true);
                    
                    storageService.AddTestDocument(document);
                }
            }
        }
    }

    private static void SetupRecentFilingsTestData(MockCrawlStorageService storageService)
    {
        var companies = new[] { "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc.", "Tesla Inc." };
        var forms = new[] { "10-Q", "8-K" };
        
        foreach (var company in companies)
        {
            foreach (var form in forms)
            {
                // Create filings for the last 90 days
                for (int i = 0; i < 10; i++)
                {
                    var filingDate = DateTime.Now.AddDays(-i * 9); // Every 9 days
                    var document = TestDataBuilder.CreateDocumentInfo(
                        companyName: company,
                        form: form,
                        filingDate: filingDate,
                        processed: true);
                    
                    storageService.AddTestDocument(document);
                }
            }
        }
    }
}

/// <summary>
/// Test execution metrics and reporting
/// </summary>
public class TestMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public int TestsExecuted { get; set; }
    public int TestsPassed { get; set; }
    public int TestsFailed { get; set; }
    public Dictionary<string, TimeSpan> TestExecutionTimes { get; set; } = new();
    public List<string> FailedTests { get; set; } = new();
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    public double SuccessRate => TestsExecuted > 0 ? (double)TestsPassed / TestsExecuted * 100 : 0;

    public void RecordTestExecution(string testName, TimeSpan executionTime, bool passed)
    {
        TestsExecuted++;
        TestExecutionTimes[testName] = executionTime;
        
        if (passed)
        {
            TestsPassed++;
        }
        else
        {
            TestsFailed++;
            FailedTests.Add(testName);
        }
    }

    public string GenerateReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Test Execution Report ===");
        report.AppendLine($"Execution Time: {Duration.TotalSeconds:F2} seconds");
        report.AppendLine($"Tests Executed: {TestsExecuted}");
        report.AppendLine($"Tests Passed: {TestsPassed}");
        report.AppendLine($"Tests Failed: {TestsFailed}");
        report.AppendLine($"Success Rate: {SuccessRate:F1}%");
        
        if (FailedTests.Any())
        {
            report.AppendLine();
            report.AppendLine("Failed Tests:");
            foreach (var failedTest in FailedTests)
            {
                report.AppendLine($"  - {failedTest}");
            }
        }

        if (TestExecutionTimes.Any())
        {
            report.AppendLine();
            report.AppendLine("Performance Summary:");
            report.AppendLine($"  Average Test Time: {TestExecutionTimes.Values.Average(t => t.TotalMilliseconds):F2}ms");
            report.AppendLine($"  Fastest Test: {TestExecutionTimes.Values.Min(t => t.TotalMilliseconds):F2}ms");
            report.AppendLine($"  Slowest Test: {TestExecutionTimes.Values.Max(t => t.TotalMilliseconds):F2}ms");
        }

        return report.ToString();
    }
}