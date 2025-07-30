using ApiGraphActivator.McpTools;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.UnitTests.McpTools;

public class CompanySearchToolTests
{
    private readonly Mock<ILogger<CompanySearchTool>> _loggerMock;
    private readonly Mock<ILogger<DocumentSearchService>> _searchServiceLoggerMock;
    private readonly MockCrawlStorageService _mockStorageService;
    private readonly DocumentSearchService _documentSearchService;
    private readonly CompanySearchTool _companySearchTool;

    public CompanySearchToolTests()
    {
        _loggerMock = new Mock<ILogger<CompanySearchTool>>();
        _searchServiceLoggerMock = new Mock<ILogger<DocumentSearchService>>();
        _mockStorageService = new MockCrawlStorageService();
        _documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, _mockStorageService);
        _companySearchTool = new CompanySearchTool(_documentSearchService, _loggerMock.Object);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _companySearchTool.Name.Should().Be("search_documents_by_company");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Act & Assert
        _companySearchTool.Description.Should().NotBeNullOrEmpty();
        _companySearchTool.Description.Should().Contain("Search SEC filing documents by company name");
    }

    [Fact]
    public void InputSchema_ShouldHaveRequiredProperties()
    {
        // Act
        var schema = _companySearchTool.InputSchema;

        // Assert
        schema.Should().NotBeNull();
        var schemaString = System.Text.Json.JsonSerializer.Serialize(schema);
        schemaString.Should().Contain("companyName");
        schemaString.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidParameters_ShouldReturnSuccessResponse()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.Content.Items.Should().NotBeEmpty();
        result.Metadata.Should().ContainKey("searchType");
        result.Metadata!["searchType"].Should().Be("company");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCompanyName_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateCompanySearchParameters("");

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFormTypes_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple", 
            formTypes: new List<string> { "INVALID-FORM" });

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Invalid form types");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidFormTypes_ShouldIncludeInMetadata()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var formTypes = new List<string> { "10-K", "10-Q" };
        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple", 
            formTypes: formTypes);

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("formTypes");
        result.Metadata!["formTypes"].Should().BeEquivalentTo(formTypes);
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
        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            startDate: startDate,
            endDate: endDate);

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("startDate");
        result.Metadata.Should().ContainKey("endDate");
        result.Metadata!["startDate"].Should().Be(startDate.ToString("yyyy-MM-dd"));
        result.Metadata!["endDate"].Should().Be(endDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task ExecuteAsync_WithPagination_ShouldRespectPageSize()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(10);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateCompanySearchParameters(
            "Apple",
            page: 1,
            pageSize: 3);

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.Content.PageSize.Should().Be(3);
        result.Content.Page.Should().Be(1);
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
        var companySearchTool = new CompanySearchTool(documentSearchService, _loggerMock.Object);

        var parameters = TestDataBuilder.CreateCompanySearchParameters("Apple");

        // Act
        var result = await companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Search failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithInvalidCompanyName_ShouldReturnValidationError(string companyName)
    {
        // Arrange
        var parameters = new CompanySearchParameters
        {
            CompanyName = companyName,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _companySearchTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Validation failed");
    }
}