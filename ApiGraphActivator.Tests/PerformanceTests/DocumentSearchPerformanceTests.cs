using ApiGraphActivator.McpTools;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ApiGraphActivator.Tests.PerformanceTests;

/// <summary>
/// Performance tests for MCP document search operations
/// Note: Using custom performance testing instead of NBomber for simplicity
/// </summary>
public class DocumentSearchPerformanceTests
{
    private readonly Mock<ILogger<DocumentSearchService>> _searchServiceLoggerMock;
    private readonly Mock<ILogger<CompanySearchTool>> _companySearchLoggerMock;
    private readonly Mock<ILogger<FormFilterTool>> _formFilterLoggerMock;
    private readonly Mock<ILogger<ContentSearchTool>> _contentSearchLoggerMock;

    public DocumentSearchPerformanceTests()
    {
        _searchServiceLoggerMock = new Mock<ILogger<DocumentSearchService>>();
        _companySearchLoggerMock = new Mock<ILogger<CompanySearchTool>>();
        _formFilterLoggerMock = new Mock<ILogger<FormFilterTool>>();
        _contentSearchLoggerMock = new Mock<ILogger<ContentSearchTool>>();
    }

    [Fact]
    public async Task CompanySearch_Performance_ShouldMeetBaseline()
    {
        // Arrange - Create large dataset for performance testing
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(1000);
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var companySearchTool = new CompanySearchTool(documentSearchService, _companySearchLoggerMock.Object);

        // Act - Run multiple searches and measure performance
        var stopwatch = Stopwatch.StartNew();
        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");
        
        for (int i = 0; i < 50; i++)
        {
            var result = await companySearchTool.ExecuteAsync(parameters);
            result.IsError.Should().BeFalse();
        }
        
        stopwatch.Stop();
        var averageTimeMs = stopwatch.ElapsedMilliseconds / 50.0;

        // Assert - Performance requirements
        averageTimeMs.Should().BeLessOrEqualTo(200); // Average response time < 200ms
    }

    [Fact]
    public async Task FormFilter_Performance_ShouldMeetBaseline()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(1000);
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var formFilterTool = new FormFilterTool(documentSearchService, _formFilterLoggerMock.Object);

        // Act - Performance test
        var stopwatch = Stopwatch.StartNew();
        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K", "10-Q" });
        
        for (int i = 0; i < 50; i++)
        {
            var result = await formFilterTool.ExecuteAsync(parameters);
            result.IsError.Should().BeFalse();
        }
        
        stopwatch.Stop();
        var averageTimeMs = stopwatch.ElapsedMilliseconds / 50.0;

        // Assert
        averageTimeMs.Should().BeLessOrEqualTo(200);
    }

    [Fact]
    public async Task ContentSearch_Performance_ShouldMeetBaseline()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(500); // Smaller dataset for content search
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var contentSearchTool = new ContentSearchTool(documentSearchService, _contentSearchLoggerMock.Object);

        // Act - Performance test
        var stopwatch = Stopwatch.StartNew();
        var parameters = TestDataBuilder.CreateContentSearchParameters("revenue");
        
        for (int i = 0; i < 25; i++) // Fewer iterations for content search
        {
            var result = await contentSearchTool.ExecuteAsync(parameters);
            result.IsError.Should().BeFalse();
        }
        
        stopwatch.Stop();
        var averageTimeMs = stopwatch.ElapsedMilliseconds / 25.0;

        // Assert - Content search has higher acceptable latency due to processing complexity
        averageTimeMs.Should().BeLessOrEqualTo(1000); // Average response time < 1000ms
    }

    [Fact]
    public async Task ConcurrentUsers_LoadTest_ShouldHandleMultipleSimultaneousRequests()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(2000);
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var companySearchTool = new CompanySearchTool(documentSearchService, _companySearchLoggerMock.Object);
        var formFilterTool = new FormFilterTool(documentSearchService, _formFilterLoggerMock.Object);

        // Act - Simulate concurrent requests
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Company search tasks
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var parameters = TestDataBuilder.CreateCompanySearchParameters("Inc");
                var result = await companySearchTool.ExecuteAsync(parameters);
                result.IsError.Should().BeFalse();
            }));
        }

        // Form filter tasks
        for (int i = 0; i < 15; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var parameters = TestDataBuilder.CreateFormFilterParameters();
                var result = await formFilterTool.ExecuteAsync(parameters);
                result.IsError.Should().BeFalse();
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All tasks should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(10000); // 10 seconds for all concurrent tasks
    }

    [Fact]
    public async Task Memory_Usage_Test_ShouldNotExceedBaseline()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(5000); // Large dataset
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var companySearchTool = new CompanySearchTool(documentSearchService, _companySearchLoggerMock.Object);

        // Act - Perform many operations to test memory usage
        var initialMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 100; i++)
        {
            var parameters = TestDataBuilder.CreateCompanySearchParameters($"Company {i % 10}");
            await companySearchTool.ExecuteAsync(parameters);
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory increase should be reasonable (less than 50MB for 100 operations)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024); // 50MB
    }

    [Fact]
    public async Task Pagination_Performance_ShouldScaleLinearly()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(10000);
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var companySearchTool = new CompanySearchTool(documentSearchService, _companySearchLoggerMock.Object);

        // Act & Assert - Test different page sizes
        var pageSizes = new[] { 10, 50, 100, 500 };
        var responseTimes = new Dictionary<int, long>();

        foreach (var pageSize in pageSizes)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var parameters = TestDataBuilder.CreateCompanySearchParameters("Inc", pageSize: pageSize);
            var result = await companySearchTool.ExecuteAsync(parameters);
            
            stopwatch.Stop();
            responseTimes[pageSize] = stopwatch.ElapsedMilliseconds;

            result.IsError.Should().BeFalse();
            result.Content.Items.Count.Should().BeLessOrEqualTo(pageSize);
        }

        // Assert - Response times should scale reasonably (not exponentially)
        // The time for 500 items should not be more than 20x the time for 10 items
        var ratio = (double)responseTimes[500] / Math.Max(responseTimes[10], 1);
        ratio.Should().BeLessThan(20);
    }

    [Fact]
    public async Task StorageService_Performance_ShouldMeetBaseline()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        
        // Act - Test storage operations performance
        var stopwatch = Stopwatch.StartNew();
        
        // Add 1000 documents
        for (int i = 0; i < 1000; i++)
        {
            var doc = TestDataBuilder.CreateDocumentInfo($"Company {i}", "10-K", DateTime.Now.AddDays(-i));
            mockStorageService.AddTestDocument(doc);
        }
        
        stopwatch.Stop();
        var addTime = stopwatch.ElapsedMilliseconds;

        // Test search performance
        stopwatch.Restart();
        
        for (int i = 0; i < 100; i++)
        {
            await mockStorageService.SearchByCompanyAsync($"Company {i % 10}", take: 10);
        }
        
        stopwatch.Stop();
        var searchTime = stopwatch.ElapsedMilliseconds;

        // Assert - Operations should complete in reasonable time
        addTime.Should().BeLessThan(1000); // Adding 1000 documents should take < 1 second
        searchTime.Should().BeLessThan(500); // 100 searches should take < 500ms
    }

    [Fact]
    public async Task Throughput_Test_ShouldMeetTargetRequests()
    {
        // Arrange
        var mockStorageService = new MockCrawlStorageService();
        var testDocuments = TestDataBuilder.CreateTestDocuments(1000);
        foreach (var doc in testDocuments)
        {
            mockStorageService.AddTestDocument(doc);
        }

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService);
        var companySearchTool = new CompanySearchTool(documentSearchService, _companySearchLoggerMock.Object);

        // Act - Measure throughput over 30 seconds
        var endTime = DateTime.Now.AddSeconds(30);
        var requestCount = 0;
        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");

        while (DateTime.Now < endTime)
        {
            var result = await companySearchTool.ExecuteAsync(parameters);
            result.IsError.Should().BeFalse();
            requestCount++;
        }

        var requestsPerSecond = requestCount / 30.0;

        // Assert - Should handle at least 10 requests per second
        requestsPerSecond.Should().BeGreaterOrEqualTo(10);
    }
}