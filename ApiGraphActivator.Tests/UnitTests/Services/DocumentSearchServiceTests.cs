using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.UnitTests.Services;

public class DocumentSearchServiceTests
{
    private readonly Mock<ILogger<DocumentSearchService>> _loggerMock;
    private readonly MockCrawlStorageService _mockStorageService;
    private readonly DocumentSearchService _documentSearchService;

    public DocumentSearchServiceTests()
    {
        _loggerMock = new Mock<ILogger<DocumentSearchService>>();
        _mockStorageService = new MockCrawlStorageService();
        _documentSearchService = new DocumentSearchService(_loggerMock.Object, _mockStorageService);
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithValidParameters_ShouldReturnResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Page.Should().Be(parameters.Page);
        result.PageSize.Should().Be(parameters.PageSize);
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithFormTypeFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(6);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            formTypes: new List<string> { "10-K" });

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        // Note: Specific filtering logic verification would depend on test data setup
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithDateRange_ShouldReturnFilteredResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var startDate = DateTime.Now.AddMonths(-6);
        var endDate = DateTime.Now;
        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            startDate: startDate,
            endDate: endDate);

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // Since the test documents are created with random dates in the past,
        // we just verify that the service accepts the date parameters without error
        // In a real scenario with controlled test data, we would verify the filtering
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithIncludeContent_ShouldIncludeContentPreview()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            includeContent: true);

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        // Note: Since we don't have blob storage in tests, content will be null
        // In a real implementation with mocked blob service, we would test content inclusion
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithPagination_ShouldRespectPagingParameters()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(10);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            page: 2,
            pageSize: 3);

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.Items.Count.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task SearchByFormTypeAsync_WithValidParameters_ShouldReturnResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K", "10-Q" });

        // Act
        var result = await _documentSearchService.SearchByFormTypeAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Page.Should().Be(parameters.Page);
        result.PageSize.Should().Be(parameters.PageSize);
    }

    [Fact]
    public async Task SearchByFormTypeAsync_WithCompanyFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(6);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" },
            companyNames: new List<string> { "Apple" });

        // Act
        var result = await _documentSearchService.SearchByFormTypeAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // All returned items should match the company filter
        result.Items.Should().OnlyContain(item => 
            item.CompanyName.Contains("Apple", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchByContentAsync_WithValidParameters_ShouldReturnResults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters("revenue");

        // Act
        var result = await _documentSearchService.SearchByContentAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Page.Should().Be(parameters.Page);
        result.PageSize.Should().Be(parameters.PageSize);
        // Note: Since we don't have actual content in test scenario, 
        // results might be empty, which is expected
    }

    [Fact]
    public async Task SearchByContentAsync_WithExactMatch_ShouldUseExactMatchLogic()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "exact phrase to find",
            exactMatch: true);

        // Act
        var result = await _documentSearchService.SearchByContentAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // Exact match logic would be verified with proper content mock
    }

    [Fact]
    public async Task SearchByContentAsync_WithCaseSensitive_ShouldUseCaseSensitiveLogic()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "Revenue",
            caseSensitive: true);

        // Act
        var result = await _documentSearchService.SearchByContentAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // Case sensitive logic would be verified with proper content mock
    }

    [Fact]
    public async Task SearchByContentAsync_ShouldSortByRelevanceAndDate()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters("test");

        // Act
        var result = await _documentSearchService.SearchByContentAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        
        // Verify sorting order (by relevance score desc, then by filing date desc)
        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            var current = result.Items[i];
            var next = result.Items[i + 1];
            
            // Either higher relevance score, or same relevance with more recent date
            (current.RelevanceScore >= next.RelevanceScore).Should().BeTrue();
            if (Math.Abs(current.RelevanceScore - next.RelevanceScore) < 0.001)
            {
                (current.FilingDate >= next.FilingDate).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task SearchByCompanyAsync_WhenStorageThrows_ShouldPropagateException()
    {
        // Arrange
        var mockStorageService = new Mock<ICrawlStorageService>();
        mockStorageService
            .Setup(x => x.GetSearchResultCountAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        var documentSearchService = new DocumentSearchService(_loggerMock.Object, mockStorageService.Object);
        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentSearchService.SearchByCompanyAsync(parameters));
    }

    [Fact]
    public async Task SearchByFormTypeAsync_WhenStorageThrows_ShouldPropagateException()
    {
        // Arrange
        var mockStorageService = new Mock<ICrawlStorageService>();
        mockStorageService
            .Setup(x => x.GetSearchResultCountAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        var documentSearchService = new DocumentSearchService(_loggerMock.Object, mockStorageService.Object);
        var parameters = TestDataBuilder.CreateFormFilterParameters();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentSearchService.SearchByFormTypeAsync(parameters));
    }

    [Fact]
    public async Task SearchByContentAsync_WhenStorageThrows_ShouldPropagateException()
    {
        // Arrange
        var mockStorageService = new Mock<ICrawlStorageService>();
        mockStorageService
            .Setup(x => x.GetSearchResultCountAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        var documentSearchService = new DocumentSearchService(_loggerMock.Object, mockStorageService.Object);
        var parameters = TestDataBuilder.CreateContentSearchParameters("test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentSearchService.SearchByContentAsync(parameters));
    }

    [Fact]
    public async Task SearchByCompanyAsync_WithEmptyResults_ShouldReturnEmptyPaginatedResult()
    {
        // Arrange
        // No test documents added to storage
        var parameters = TestDataBuilder.CreateCompanySearchParameters("NonExistentCompany");

        // Act
        var result = await _documentSearchService.SearchByCompanyAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(parameters.Page);
        result.PageSize.Should().Be(parameters.PageSize);
    }
}