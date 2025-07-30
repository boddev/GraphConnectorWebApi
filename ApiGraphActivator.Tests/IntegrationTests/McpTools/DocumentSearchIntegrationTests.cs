using ApiGraphActivator.McpTools;
using ApiGraphActivator.Services;
using ApiGraphActivator.Tests.Mocks;
using ApiGraphActivator.Tests.TestData;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Tests.IntegrationTests.McpTools;

/// <summary>
/// Integration tests for MCP document search tools working together
/// </summary>
public class DocumentSearchIntegrationTests
{
    private readonly Mock<ILogger<DocumentSearchService>> _searchServiceLoggerMock;
    private readonly Mock<ILogger<CompanySearchTool>> _companySearchLoggerMock;
    private readonly Mock<ILogger<FormFilterTool>> _formFilterLoggerMock;
    private readonly Mock<ILogger<ContentSearchTool>> _contentSearchLoggerMock;
    private readonly MockCrawlStorageService _mockStorageService;
    private readonly DocumentSearchService _documentSearchService;
    private readonly CompanySearchTool _companySearchTool;
    private readonly FormFilterTool _formFilterTool;
    private readonly ContentSearchTool _contentSearchTool;

    public DocumentSearchIntegrationTests()
    {
        _searchServiceLoggerMock = new Mock<ILogger<DocumentSearchService>>();
        _companySearchLoggerMock = new Mock<ILogger<CompanySearchTool>>();
        _formFilterLoggerMock = new Mock<ILogger<FormFilterTool>>();
        _contentSearchLoggerMock = new Mock<ILogger<ContentSearchTool>>();
        _mockStorageService = new MockCrawlStorageService();
        _documentSearchService = new DocumentSearchService(_searchServiceLoggerMock.Object, _mockStorageService);
        _companySearchTool = new CompanySearchTool(_documentSearchService, _companySearchLoggerMock.Object);
        _formFilterTool = new FormFilterTool(_documentSearchService, _formFilterLoggerMock.Object);
        _contentSearchTool = new ContentSearchTool(_documentSearchService, _contentSearchLoggerMock.Object);
    }

    [Fact]
    public async Task EndToEndDocumentSearch_WithMultipleCompanies_ShouldReturnResults()
    {
        // Arrange - Create realistic test dataset
        var companies = new[] { "Apple Inc.", "Microsoft Corporation", "Amazon.com Inc." };
        var forms = new[] { "10-K", "10-Q", "8-K" };
        
        foreach (var company in companies)
        {
            foreach (var form in forms)
            {
                for (int i = 0; i < 3; i++)
                {
                    var document = TestDataBuilder.CreateDocumentInfo(
                        companyName: company,
                        form: form,
                        filingDate: DateTime.Now.AddMonths(-i * 2),
                        processed: true);
                    _mockStorageService.AddTestDocument(document);
                }
            }
        }

        // Act & Assert - Test company search
        var companySearchParams = TestDataBuilder.CreateCompanySearchParameters("Apple");
        var companyResults = await _companySearchTool.ExecuteAsync(companySearchParams);
        
        companyResults.Should().NotBeNull();
        companyResults.IsError.Should().BeFalse();
        companyResults.Content.Items.Should().NotBeEmpty();
        companyResults.Content.Items.Should().OnlyContain(r => r.CompanyName.Contains("Apple"));

        // Act & Assert - Test form filter
        var formFilterParams = TestDataBuilder.CreateFormFilterParameters(
            formTypes: new List<string> { "10-K" });
        var formResults = await _formFilterTool.ExecuteAsync(formFilterParams);
        
        formResults.Should().NotBeNull();
        formResults.IsError.Should().BeFalse();
        formResults.Content.Items.Should().NotBeEmpty();
        formResults.Content.Items.Should().OnlyContain(r => r.FormType == "10-K");

        // Act & Assert - Test content search (will return empty due to no actual content, but validates the workflow)
        var contentSearchParams = TestDataBuilder.CreateContentSearchParameters("revenue");
        var contentResults = await _contentSearchTool.ExecuteAsync(contentSearchParams);
        
        contentResults.Should().NotBeNull();
        contentResults.IsError.Should().BeFalse();
        // Content results may be empty due to lack of actual content in mock storage
    }

    [Fact]
    public async Task MCP_Tools_Schema_Validation_ShouldBeConsistent()
    {
        // Arrange & Act - Get schemas from all tools
        var companySchema = _companySearchTool.InputSchema;
        var formSchema = _formFilterTool.InputSchema;
        var contentSchema = _contentSearchTool.InputSchema;

        // Assert - All schemas should have consistent pagination properties
        var companySchemaString = System.Text.Json.JsonSerializer.Serialize(companySchema);
        var formSchemaString = System.Text.Json.JsonSerializer.Serialize(formSchema);
        var contentSchemaString = System.Text.Json.JsonSerializer.Serialize(contentSchema);

        // All should have page and pageSize properties
        companySchemaString.Should().Contain("page");
        companySchemaString.Should().Contain("pageSize");
        formSchemaString.Should().Contain("page");
        formSchemaString.Should().Contain("pageSize");
        contentSchemaString.Should().Contain("page");
        contentSchemaString.Should().Contain("pageSize");

        // All should have date range properties
        companySchemaString.Should().Contain("startDate");
        companySchemaString.Should().Contain("endDate");
        formSchemaString.Should().Contain("startDate");
        formSchemaString.Should().Contain("endDate");
        contentSchemaString.Should().Contain("startDate");
        contentSchemaString.Should().Contain("endDate");
    }

    [Fact]
    public async Task MCP_Tools_Response_Format_ShouldBeConsistent()
    {
        // Arrange
        var testDocuments = TestDataBuilder.CreateTestDocuments(5);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        // Act - Execute all tools
        var companyResult = await _companySearchTool.ExecuteAsync(
            TestDataBuilder.CreateCompanySearchParameters("Apple"));
        var formResult = await _formFilterTool.ExecuteAsync(
            TestDataBuilder.CreateFormFilterParameters());
        var contentResult = await _contentSearchTool.ExecuteAsync(
            TestDataBuilder.CreateContentSearchParameters("test"));

        // Assert - All should have consistent response structure
        companyResult.Should().NotBeNull();
        companyResult.IsError.Should().BeFalse();
        companyResult.Content.Should().NotBeNull();
        companyResult.Metadata.Should().NotBeNull();
        companyResult.Metadata.Should().ContainKey("searchType");
        companyResult.Metadata.Should().ContainKey("executionTime");

        formResult.Should().NotBeNull();
        formResult.IsError.Should().BeFalse();
        formResult.Content.Should().NotBeNull();
        formResult.Metadata.Should().NotBeNull();
        formResult.Metadata.Should().ContainKey("searchType");
        formResult.Metadata.Should().ContainKey("executionTime");

        contentResult.Should().NotBeNull();
        contentResult.IsError.Should().BeFalse();
        contentResult.Content.Should().NotBeNull();
        contentResult.Metadata.Should().NotBeNull();
        contentResult.Metadata.Should().ContainKey("searchType");
        contentResult.Metadata.Should().ContainKey("executionTime");
    }

    [Fact]
    public async Task CrossTool_Search_Results_ShouldBeLogicallyConsistent()
    {
        // Arrange - Create test data with specific characteristics
        var appleDocuments = new[]
        {
            TestDataBuilder.CreateDocumentInfo("Apple Inc.", "10-K", DateTime.Now.AddDays(-30)),
            TestDataBuilder.CreateDocumentInfo("Apple Inc.", "10-Q", DateTime.Now.AddDays(-60)),
            TestDataBuilder.CreateDocumentInfo("Apple Inc.", "8-K", DateTime.Now.AddDays(-90))
        };

        var microsoftDocuments = new[]
        {
            TestDataBuilder.CreateDocumentInfo("Microsoft Corporation", "10-K", DateTime.Now.AddDays(-45)),
            TestDataBuilder.CreateDocumentInfo("Microsoft Corporation", "10-Q", DateTime.Now.AddDays(-75))
        };

        foreach (var doc in appleDocuments.Concat(microsoftDocuments))
        {
            _mockStorageService.AddTestDocument(doc);
        }

        // Act - Search by company vs search by form type should have logical relationship
        var appleCompanySearch = await _companySearchTool.ExecuteAsync(
            TestDataBuilder.CreateCompanySearchParameters("Apple"));
        
        var tenKFormSearch = await _formFilterTool.ExecuteAsync(
            TestDataBuilder.CreateFormFilterParameters(
                formTypes: new List<string> { "10-K" }));

        // Assert - Results should be logically consistent
        appleCompanySearch.Content.Items.Should().HaveCount(3); // 3 Apple documents
        appleCompanySearch.Content.Items.Should().OnlyContain(r => r.CompanyName.Contains("Apple"));

        tenKFormSearch.Content.Items.Should().HaveCount(2); // 2 10-K documents (Apple + Microsoft)
        tenKFormSearch.Content.Items.Should().OnlyContain(r => r.FormType == "10-K");

        // The intersection (Apple 10-K documents) should be 1
        var appleTenKDocuments = appleCompanySearch.Content.Items
            .Where(r => r.FormType == "10-K")
            .ToList();
        appleTenKDocuments.Should().HaveCount(1);
    }

    [Fact]
    public async Task PaginationConsistency_AcrossAllTools_ShouldWork()
    {
        // Arrange - Create enough test data for pagination
        var testDocuments = TestDataBuilder.CreateTestDocuments(15);
        foreach (var doc in testDocuments)
        {
            _mockStorageService.AddTestDocument(doc);
        }

        var pageSize = 5;

        // Act - Test pagination on all tools
        var companyPage1 = await _companySearchTool.ExecuteAsync(
            TestDataBuilder.CreateCompanySearchParameters("Inc", page: 1, pageSize: pageSize));
        var companyPage2 = await _companySearchTool.ExecuteAsync(
            TestDataBuilder.CreateCompanySearchParameters("Inc", page: 2, pageSize: pageSize));

        var formPage1 = await _formFilterTool.ExecuteAsync(
            TestDataBuilder.CreateFormFilterParameters(page: 1, pageSize: pageSize));
        var formPage2 = await _formFilterTool.ExecuteAsync(
            TestDataBuilder.CreateFormFilterParameters(page: 2, pageSize: pageSize));

        // Assert - Pagination should work consistently
        companyPage1.Content.Page.Should().Be(1);
        companyPage1.Content.PageSize.Should().Be(pageSize);
        companyPage1.Content.Items.Count.Should().BeLessOrEqualTo(pageSize);

        companyPage2.Content.Page.Should().Be(2);
        companyPage2.Content.PageSize.Should().Be(pageSize);

        formPage1.Content.Page.Should().Be(1);
        formPage1.Content.PageSize.Should().Be(pageSize);

        formPage2.Content.Page.Should().Be(2);
        formPage2.Content.PageSize.Should().Be(pageSize);

        // Pages should not have duplicate items
        var page1Ids = companyPage1.Content.Items.Select(i => i.Id).ToList();
        var page2Ids = companyPage2.Content.Items.Select(i => i.Id).ToList();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Theory]
    [InlineData("10-K")]
    [InlineData("10-Q")]
    [InlineData("8-K")]
    [InlineData("10-K/A")]
    [InlineData("10-Q/A")]
    [InlineData("8-K/A")]
    public async Task AllValidFormTypes_ShouldBeSupported_AcrossAllTools(string formType)
    {
        // Arrange
        var testDocument = TestDataBuilder.CreateDocumentInfo(
            "Test Company", formType, DateTime.Now.AddDays(-30));
        _mockStorageService.AddTestDocument(testDocument);

        // Act & Assert - Company search with form type filter
        var companyResult = await _companySearchTool.ExecuteAsync(
            TestDataBuilder.CreateCompanySearchParameters(
                "Test Company", 
                formTypes: new List<string> { formType }));
        
        companyResult.IsError.Should().BeFalse();

        // Act & Assert - Form filter search
        var formResult = await _formFilterTool.ExecuteAsync(
            TestDataBuilder.CreateFormFilterParameters(
                formTypes: new List<string> { formType }));
        
        formResult.IsError.Should().BeFalse();

        // Act & Assert - Content search with form type filter
        var contentResult = await _contentSearchTool.ExecuteAsync(
            TestDataBuilder.CreateContentSearchParameters(
                "test",
                formTypes: new List<string> { formType }));
        
        contentResult.IsError.Should().BeFalse();
    }
}