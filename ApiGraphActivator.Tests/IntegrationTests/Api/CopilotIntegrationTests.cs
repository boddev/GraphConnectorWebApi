using ApiGraphActivator.Tests.Mocks;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.IntegrationTests.Api;

/// <summary>
/// Integration tests for M365 Copilot integration and Graph API operations
/// </summary>
public class CopilotIntegrationTests
{
    private readonly Mock<ILogger<MockGraphService>> _loggerMock;
    private readonly MockGraphService _mockGraphService;

    public CopilotIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<MockGraphService>>();
        _mockGraphService = new MockGraphService(_loggerMock.Object);
    }

    [Fact]
    public async Task GraphService_Authentication_ShouldWorkCorrectly()
    {
        // Arrange
        _mockGraphService.SetAuthenticated(false);

        // Act
        var initialAuthStatus = await _mockGraphService.IsAuthenticatedAsync();
        var authResult = await _mockGraphService.AuthenticateAsync();
        var finalAuthStatus = await _mockGraphService.IsAuthenticatedAsync();

        // Assert
        initialAuthStatus.Should().BeFalse();
        authResult.Should().BeTrue();
        finalAuthStatus.Should().BeTrue();
    }

    [Fact]
    public async Task GraphService_GetUserDisplayName_WhenAuthenticated_ShouldReturnName()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();

        // Act
        var displayName = await _mockGraphService.GetUserDisplayNameAsync();

        // Assert
        displayName.Should().NotBeNullOrEmpty();
        displayName.Should().Be("Mock User");
    }

    [Fact]
    public async Task GraphService_GetUserDisplayName_WhenNotAuthenticated_ShouldThrow()
    {
        // Arrange
        _mockGraphService.SetAuthenticated(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _mockGraphService.GetUserDisplayNameAsync());
    }

    [Fact]
    public async Task GraphService_CreateExternalConnection_ShouldSucceed()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "test-connection";
        var name = "Test Connection";
        var description = "Test connection for SEC Edgar data";

        // Act
        var result = await _mockGraphService.CreateExternalConnectionAsync(connectionId, name, description);

        // Assert
        result.Should().BeTrue();
        
        // Verify connection status
        var status = await _mockGraphService.GetConnectionStatusAsync(connectionId);
        status.Should().ContainKey("id");
        status["id"].Should().Be(connectionId);
        status.Should().ContainKey("state");
        status["state"].Should().Be("ready");
    }

    [Fact]
    public async Task GraphService_IndexExternalItem_ShouldSucceed()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "test-connection";
        var itemId = "test-item-1";
        
        await _mockGraphService.CreateExternalConnectionAsync(connectionId, "Test Connection", "Test description");

        var properties = new Dictionary<string, object>
        {
            ["title"] = "Apple Inc. 10-K 2023",
            ["company"] = "Apple Inc.",
            ["formType"] = "10-K",
            ["filingDate"] = DateTime.Now.ToString("O"),
            ["url"] = "https://www.sec.gov/test-filing"
        };
        var content = "This is the content of the SEC filing document.";

        // Act
        var result = await _mockGraphService.IndexExternalItemAsync(connectionId, itemId, properties, content);

        // Assert
        result.Should().BeTrue();
        
        // Verify item was indexed
        var indexedItem = await _mockGraphService.GetExternalItemAsync(connectionId, itemId);
        indexedItem.Should().NotBeNull();
        indexedItem!.Should().ContainKey("Properties");
        
        // Verify connection status shows the item
        var status = await _mockGraphService.GetConnectionStatusAsync(connectionId);
        status["itemsCount"].Should().Be(1);
    }

    [Fact]
    public async Task GraphService_SearchExternalContent_ShouldReturnResults()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "test-connection";
        var itemId = "test-item-1";
        
        await _mockGraphService.CreateExternalConnectionAsync(connectionId, "Test Connection", "Test description");
        
        var properties = new Dictionary<string, object>
        {
            ["title"] = "Apple Inc. Revenue Growth",
            ["company"] = "Apple Inc.",
            ["formType"] = "10-K"
        };
        await _mockGraphService.IndexExternalItemAsync(connectionId, itemId, properties, "Revenue increased by 25%");

        // Act
        var searchResults = await _mockGraphService.SearchExternalContentAsync("revenue");

        // Assert
        searchResults.Should().NotBeNull();
        searchResults.Should().ContainKey("searchTerms");
        searchResults.Should().ContainKey("hitsContainers");
        
        var searchTerms = searchResults["searchTerms"] as string[];
        searchTerms.Should().Contain("revenue");
    }

    [Fact]
    public async Task GraphService_DeleteExternalItem_ShouldSucceed()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "test-connection";
        var itemId = "test-item-1";
        
        await _mockGraphService.CreateExternalConnectionAsync(connectionId, "Test Connection", "Test description");
        await _mockGraphService.IndexExternalItemAsync(connectionId, itemId, new Dictionary<string, object>(), "test content");

        // Act
        var deleteResult = await _mockGraphService.DeleteExternalItemAsync(connectionId, itemId);

        // Assert
        deleteResult.Should().BeTrue();
        
        // Verify item was deleted
        var deletedItem = await _mockGraphService.GetExternalItemAsync(connectionId, itemId);
        deletedItem.Should().BeNull();
        
        // Verify connection status reflects deletion
        var status = await _mockGraphService.GetConnectionStatusAsync(connectionId);
        status["itemsCount"].Should().Be(0);
    }

    [Fact]
    public async Task GraphService_GetConnectionItems_ShouldReturnIndexedItems()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "test-connection";
        
        await _mockGraphService.CreateExternalConnectionAsync(connectionId, "Test Connection", "Test description");

        // Index multiple items
        for (int i = 1; i <= 5; i++)
        {
            var properties = new Dictionary<string, object>
            {
                ["title"] = $"Document {i}",
                ["company"] = $"Company {i}",
                ["formType"] = "10-K"
            };
            await _mockGraphService.IndexExternalItemAsync(connectionId, $"item-{i}", properties, $"Content {i}");
        }

        // Act
        var items = await _mockGraphService.GetConnectionItemsAsync(connectionId);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(5);
        items.Should().OnlyContain(item => item.ContainsKey("Properties"));
    }

    [Fact]
    public async Task CopilotWorkflow_EndToEnd_ShouldWorkCorrectly()
    {
        // Arrange - Simulate a complete Copilot integration workflow
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "sec-edgar-connector";
        
        // Act & Assert - Step 1: Create connection
        var connectionResult = await _mockGraphService.CreateExternalConnectionAsync(
            connectionId, 
            "SEC Edgar Document Connector", 
            "Provides access to SEC filing documents");
        connectionResult.Should().BeTrue();

        // Act & Assert - Step 2: Index SEC documents
        var secDocuments = new[]
        {
            new { Id = "apple-10k-2023", Title = "Apple Inc. 10-K 2023", Company = "Apple Inc.", Form = "10-K", Content = "Apple's annual report shows strong revenue growth..." },
            new { Id = "msft-10q-2023", Title = "Microsoft Corporation 10-Q Q3 2023", Company = "Microsoft Corporation", Form = "10-Q", Content = "Microsoft's quarterly results exceed expectations..." },
            new { Id = "amzn-8k-2023", Title = "Amazon.com Inc. 8-K Current Report", Company = "Amazon.com Inc.", Form = "8-K", Content = "Amazon announces new AWS services..." }
        };

        foreach (var doc in secDocuments)
        {
            var properties = new Dictionary<string, object>
            {
                ["title"] = doc.Title,
                ["company"] = doc.Company,
                ["formType"] = doc.Form,
                ["filingDate"] = DateTime.Now.ToString("O"),
                ["url"] = $"https://www.sec.gov/filing/{doc.Id}"
            };
            
            var indexResult = await _mockGraphService.IndexExternalItemAsync(connectionId, doc.Id, properties, doc.Content);
            indexResult.Should().BeTrue();
        }

        // Act & Assert - Step 3: Verify connection status
        var finalStatus = await _mockGraphService.GetConnectionStatusAsync(connectionId);
        finalStatus["itemsCount"].Should().Be(3);
        finalStatus["state"].Should().Be("ready");

        // Act & Assert - Step 4: Simulate Copilot search queries
        var revenueSearch = await _mockGraphService.SearchExternalContentAsync("revenue growth");
        revenueSearch.Should().NotBeNull();
        revenueSearch.Should().ContainKey("hitsContainers");

        var awsSearch = await _mockGraphService.SearchExternalContentAsync("AWS services");
        awsSearch.Should().NotBeNull();
        awsSearch.Should().ContainKey("hitsContainers");

        // Act & Assert - Step 5: Retrieve all indexed items
        var allItems = await _mockGraphService.GetConnectionItemsAsync(connectionId);
        allItems.Should().HaveCount(3);
        allItems.Should().OnlyContain(item => 
            item.ContainsKey("Properties") && item.ContainsKey("Content"));
    }

    [Fact]
    public async Task CopilotIntegration_ErrorHandling_ShouldWorkCorrectly()
    {
        // Test authentication required scenarios
        _mockGraphService.SetAuthenticated(false);

        // Act & Assert - All operations should fail when not authenticated
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _mockGraphService.CreateExternalConnectionAsync("test", "test", "test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _mockGraphService.IndexExternalItemAsync("test", "test", new Dictionary<string, object>(), "test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _mockGraphService.SearchExternalContentAsync("test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _mockGraphService.GetConnectionStatusAsync("test"));

        // Test invalid connection scenarios
        await _mockGraphService.AuthenticateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _mockGraphService.GetConnectionStatusAsync("non-existent-connection"));
    }

    [Fact]
    public async Task CopilotIntegration_LargeDataset_ShouldHandleEfficiently()
    {
        // Arrange
        await _mockGraphService.AuthenticateAsync();
        var connectionId = "large-dataset-test";
        
        await _mockGraphService.CreateExternalConnectionAsync(connectionId, "Large Dataset Test", "Test with many documents");

        // Act - Index many documents
        var documentsToIndex = 100;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 1; i <= documentsToIndex; i++)
        {
            var properties = new Dictionary<string, object>
            {
                ["title"] = $"SEC Document {i}",
                ["company"] = $"Company {i % 10}",
                ["formType"] = i % 3 == 0 ? "10-K" : i % 3 == 1 ? "10-Q" : "8-K",
                ["filingDate"] = DateTime.Now.AddDays(-i).ToString("O")
            };
            
            await _mockGraphService.IndexExternalItemAsync(connectionId, $"doc-{i}", properties, $"Content for document {i}");
        }

        stopwatch.Stop();

        // Assert - Operations should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Less than 5 seconds for 100 documents

        // Verify all documents were indexed
        var status = await _mockGraphService.GetConnectionStatusAsync(connectionId);
        status["itemsCount"].Should().Be(documentsToIndex);

        // Test retrieval performance
        stopwatch.Restart();
        var allItems = await _mockGraphService.GetConnectionItemsAsync(connectionId, documentsToIndex);
        stopwatch.Stop();

        allItems.Should().HaveCount(documentsToIndex);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Less than 1 second to retrieve 100 documents
    }
}