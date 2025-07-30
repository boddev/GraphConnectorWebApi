using ApiGraphActivator.McpTools;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.UnitTests.McpTools;

public class FormFilterToolTests
{
    private readonly Mock<ILogger<FormFilterTool>> _loggerMock;
    private readonly Mock<ILogger<DocumentSearchService>> _searchServiceLoggerMock;
    private readonly MockCrawlStorageService _mockStorageService;
    private readonly DocumentSearchService _documentSearchService;
    private readonly FormFilterTool _formFilterTool;

    public FormFilterToolTests()
    {
        _loggerMock = new Mock<ILogger<FormFilterTool>>();
        _searchServiceLoggerMock = new Mock<ILogger<DocumentSearchService>>();
        _mockStorageService = new MockCrawlStorageService();
        _documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, _mockStorageService);
        _formFilterTool = new FormFilterTool(_documentSearchService, _loggerMock.Object);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _formFilterTool.Name.Should().Be("filter_documents_by_form_and_date");
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Act & Assert
        _formFilterTool.Description.Should().NotBeNullOrEmpty();
        _formFilterTool.Description.Should().Contain("Filter SEC filing documents by form type and date range");
    }

    [Fact]
    public void InputSchema_ShouldHaveCorrectProperties()
    {
        // Act
        var schema = _formFilterTool.InputSchema;

        // Assert
        schema.Should().NotBeNull();
        var schemaString = System.Text.Json.JsonSerializer.Serialize(schema);
        schemaString.Should().Contain("formTypes");
        schemaString.Should().Contain("companyNames");
        schemaString.Should().Contain("startDate");
        schemaString.Should().Contain("endDate");
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

        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" });

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.Metadata.Should().ContainKey("searchType");
        result.Metadata!["searchType"].Should().Be("formFilter");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoFormTypes_ShouldUseAllFormTypes()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        // Create parameters with explicitly null form types
        var parameters = new FormFilterParameters
        {
            FormTypes = null,
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("formTypes");
        // The actual formTypes in metadata should be all form types
        var formTypesFromMetadata = result.Metadata!["formTypes"];
        formTypesFromMetadata.Should().NotBeNull();
        
        // Convert to list for comparison
        var formTypesList = formTypesFromMetadata as IEnumerable<string>;
        formTypesList.Should().BeEquivalentTo(FormTypes.AllFormTypes);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFormTypes_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "INVALID-FORM", "ANOTHER-INVALID" });

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Invalid form types");
        result.ErrorMessage.Should().Contain("INVALID-FORM");
        result.ErrorMessage.Should().Contain("ANOTHER-INVALID");
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedValidInvalidFormTypes_ShouldReturnValidationError()
    {
        // Arrange
        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K", "INVALID-FORM", "10-Q" });

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Invalid form types");
        result.ErrorMessage.Should().Contain("INVALID-FORM");
        // Note: The error message includes all form types in the validation message, so we just check it contains the invalid one
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
        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" },
            companyNames: companyNames);

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

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
        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" },
            startDate: startDate,
            endDate: endDate);

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("startDate");
        result.Metadata.Should().ContainKey("endDate");
        result.Metadata!["startDate"].Should().Be(startDate.ToString("yyyy-MM-dd"));
        result.Metadata!["endDate"].Should().Be(endDate.ToString("yyyy-MM-dd"));
    }

    [Theory]
    [InlineData("10-K")]
    [InlineData("10-Q")]
    [InlineData("8-K")]
    [InlineData("10-K/A")]
    [InlineData("10-Q/A")]
    [InlineData("8-K/A")]
    public async Task ExecuteAsync_WithValidFormType_ShouldSucceed(string formType)
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { formType });

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithPagination_ShouldRespectPageSettings()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(10);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var parameters = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" },
            page: 2,
            pageSize: 3);

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.Content.Page.Should().Be(2);
        result.Content.PageSize.Should().Be(3);
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
        var formFilterTool = new FormFilterTool(documentSearchService, _loggerMock.Object);

        var parameters = TestDataBuilder.CreateFormFilterParameters();

        // Act
        var result = await formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Search failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyFormTypesList_ShouldUseAllFormTypes()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(3);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        // Create parameters with explicitly empty form types list
        var parameters = new FormFilterParameters
        {
            FormTypes = new List<string>(), // Empty list
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _formFilterTool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeFalse();
        result.Metadata.Should().ContainKey("formTypes");
        var formTypesFromMetadata = result.Metadata!["formTypes"];
        var formTypesList = formTypesFromMetadata as IEnumerable<string>;
        formTypesList.Should().BeEquivalentTo(FormTypes.AllFormTypes);
    }
}