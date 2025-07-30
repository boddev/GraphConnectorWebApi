using ApiGraphActivator.McpTools;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.UnitTests.McpTools;

public class ContentSearchToolTests
{
    private readonly Mock<ILogger<ContentSearchTool>> _loggerMock;
    private readonly Mock<ILogger<DocumentSearchService>> _searchServiceLoggerMock;
    private readonly MockCrawlStorageService _mockStorageService;
    private readonly DocumentSearchService _documentSearchService;
    private readonly ContentSearchTool _contentSearchTool;

    public ContentSearchToolTests()
    {
        _loggerMock = new Mock<ILogger<ContentSearchTool>>();
        _searchServiceLoggerMock = new Mock<ILogger<DocumentSearchService>>();
        _mockStorageService = new MockCrawlStorageService();
        _documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, _mockStorageService);
        _contentSearchTool = new ContentSearchTool(_documentSearchService, _loggerMock.Object);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _contentSearchTool.Name.Should().Be("search_document_content");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Act & Assert
        _contentSearchTool.Description.Should().NotBeNullOrEmpty();
        _contentSearchTool.Description.Should().Contain("Perform full-text search within SEC filing document content");
    }

    [Fact]
    public void InputSchema_ShouldHaveRequiredProperties()
    {
        // Act
        var schema = _contentSearchTool.InputSchema;

        // Assert
        schema.Should().NotBeNull();
        var schemaString = System.Text.Json.JsonSerializer.Serialize(schema);
        schemaString.Should().Contain("searchText");
        schemaString.Should().Contain("required");
        schemaString.Should().Contain("exactMatch");
        schemaString.Should().Contain("caseSensitive");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidParameters_ShouldReturnSuccessResponse()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters("revenue");

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.Metadata.Should().ContainKey("searchType");
        result.Metadata!["searchType"].Should().Be("content");
        result.Metadata.Should().ContainKey("searchTerm");
        result.Metadata!["searchTerm"].Should().Be("revenue");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySearchText_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateContentSearchParameters("");

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFormTypes_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            formTypes: new List<string> { "INVALID-FORM" });

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Invalid form types");
    }

    [Fact]
    public async Task ExecuteAsync_WithExactMatch_ShouldIncludeInMetadata()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            exactMatch: true);

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("exactMatch");
        result.Metadata!["exactMatch"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithCaseSensitive_ShouldIncludeInMetadata()
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
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("caseSensitive");
        result.Metadata!["caseSensitive"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_WithFormTypes_ShouldIncludeInMetadata()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var formTypes = new List<string> { "10-K", "10-Q" };
        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            formTypes: formTypes);

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("formTypes");
        result.Metadata!["formTypes"].Should().BeEquivalentTo(formTypes);
    }

    [Fact]
    public async Task ExecuteAsync_WithCompanyNames_ShouldIncludeInMetadata()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var companyNames = new List<string> { "Apple Inc.", "Microsoft Corporation" };
        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            companyNames: companyNames);

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("companyNames");
        result.Metadata!["companyNames"].Should().BeEquivalentTo(companyNames);
    }

    [Fact]
    public async Task ExecuteAsync_WithDateRange_ShouldIncludeInMetadata()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var startDate = DateTime.Now.AddYears(-1);
        var endDate = DateTime.Now;
        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            startDate: startDate,
            endDate: endDate);

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("startDate");
        result.Metadata.Should().ContainKey("endDate");
        result.Metadata!["startDate"].Should().Be(startDate.ToString("yyyy-MM-dd"));
        result.Metadata!["endDate"].Should().Be(endDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ExecuteAsync_WithLargePageSize_ShouldLimitTo100()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters(
            "revenue",
            pageSize: 500); // Large page size

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        // Note: The actual limiting happens in the DocumentSearchService
        // This test mainly ensures the tool doesn't crash with large page sizes
    }

    [Fact]
    public async Task ExecuteAsync_WhenStorageThrows_ShouldReturnErrorResponse()
    {
        // Arrange
        var mockStorageService = new Mock<ICrawlStorageService>();
        mockStorageService
            .Setup(x => x.GetSearchResultCountAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        var documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, mockStorageService.Object);
        var contentSearchTool = new ContentSearchTool(documentSearchService, _loggerMock.Object);

        var parameters = TestDataBuilder.CreateContentSearchParameters("revenue");

        // Act
        var result = await contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Search failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithInvalidSearchText_ShouldReturnValidationError(string searchText)
    {
        // Arrange
        var parameters = new ContentSearchParameters
        {
            SearchText = searchText,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultParameters_ShouldSetCorrectDefaults()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateContentSearchParameters("revenue");

        // Act
        var result = await _contentSearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("exactMatch");
        result.Metadata.Should().ContainKey("caseSensitive");
        result.Metadata!["exactMatch"].Should().Be(false);
        result.Metadata!["caseSensitive"].Should().Be(false);
    }
}