using ApiGraphActivator.McpTools;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace ApiGraphActivator.Tests;

/// <summary>
/// Core MCP protocol compliance tests that don't require external dependencies
/// These tests focus on the fundamental protocol structures and validation
/// </summary>
public class McpCoreProtocolTests
{
    [Fact]
    public void McpToolResponse_Success_ShouldHaveCorrectStructure()
    {
        // Arrange
        var content = "test content";
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var response = McpToolResponse<string>.Success(content, metadata);

        // Assert - MCP protocol requires specific response structure
        response.Should().NotBeNull();
        response.IsError.Should().BeFalse();
        response.ErrorMessage.Should().BeNull();
        response.Content.Should().Be(content);
        response.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void McpToolResponse_Error_ShouldHaveCorrectStructure()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var response = McpToolResponse<string>.Error(errorMessage);

        // Assert - MCP error responses must follow protocol standards
        response.Should().NotBeNull();
        response.IsError.Should().BeTrue();
        response.ErrorMessage.Should().Be(errorMessage);
        response.Content.Should().BeNull();
        response.Metadata.Should().BeNull();
    }

    [Fact]
    public void McpToolResponse_JsonSerialization_ShouldPreserveStructure()
    {
        // Arrange
        var originalResponse = McpToolResponse<string>.Success("test", new Dictionary<string, object> { ["test"] = "metadata" });

        // Act
        var json = JsonSerializer.Serialize(originalResponse);
        var deserializedResponse = JsonSerializer.Deserialize<McpToolResponse<string>>(json);

        // Assert - MCP responses must be serializable and maintain structure
        deserializedResponse.Should().NotBeNull();
        deserializedResponse!.IsError.Should().Be(originalResponse.IsError);
        deserializedResponse.Content.Should().Be(originalResponse.Content);
        deserializedResponse.ErrorMessage.Should().Be(originalResponse.ErrorMessage);
        deserializedResponse.Metadata.Should().NotBeNull();
        deserializedResponse.Metadata!["test"].ToString().Should().Be("metadata");
    }

    [Fact]
    public void McpPaginationParameters_ValidValues_ShouldPassValidation()
    {
        // Arrange
        var validParameters = new PaginationParameters
        {
            Page = 1,
            PageSize = 50
        };

        // Act
        var isValid = ValidateObject(validParameters);

        // Assert - Valid pagination parameters should pass MCP validation
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 50)] // Page 0 is invalid
    [InlineData(-1, 50)] // Negative page is invalid
    [InlineData(1, 0)] // PageSize 0 is invalid
    [InlineData(1, -1)] // Negative page size is invalid
    [InlineData(1, 1001)] // PageSize too large is invalid
    public void McpPaginationParameters_InvalidValues_ShouldFailValidation(int page, int pageSize)
    {
        // Arrange
        var invalidParameters = new PaginationParameters
        {
            Page = page,
            PageSize = pageSize
        };

        // Act
        var isValid = ValidateObject(invalidParameters);

        // Assert - Invalid pagination parameters should fail MCP validation
        isValid.Should().BeFalse($"Page={page}, PageSize={pageSize} should be invalid");
    }

    [Fact]
    public void McpPaginatedResult_CalculatesPropertiesCorrectly()
    {
        // Arrange
        var result = new PaginatedResult<string>
        {
            Items = new List<string> { "item1", "item2" },
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        // Act & Assert - MCP pagination calculations must be correct
        result.TotalPages.Should().Be(3, "25 items / 10 per page = 3 pages");
        result.HasNextPage.Should().BeTrue("Page 2 of 3 should have next page");
        result.HasPreviousPage.Should().BeTrue("Page 2 of 3 should have previous page");
    }

    [Theory]
    [InlineData("10-K", true)]
    [InlineData("10-Q", true)]
    [InlineData("8-K", true)]
    [InlineData("10-K/A", true)]
    [InlineData("10-Q/A", true)]
    [InlineData("8-K/A", true)]
    [InlineData("INVALID", false)]
    [InlineData("10-X", false)]
    [InlineData("20-F", false)]
    [InlineData("", false)]
    public void McpFormTypes_Validation_ShouldFollowProtocol(string formType, bool expectedValid)
    {
        // Act
        var isValid = FormTypes.IsValidFormType(formType);

        // Assert - MCP form type validation must be consistent
        isValid.Should().Be(expectedValid, $"Form type '{formType}' validation should be {expectedValid}");
    }

    [Fact]
    public void McpFormTypes_AllFormTypes_ShouldContainExpectedValues()
    {
        // Arrange
        var expectedFormTypes = new[] { "10-K", "10-Q", "8-K", "10-K/A", "10-Q/A", "8-K/A" };

        // Act & Assert - MCP protocol must define all required form types
        FormTypes.AllFormTypes.Should().HaveCount(6);
        FormTypes.AllFormTypes.Should().BeEquivalentTo(expectedFormTypes);
    }

    [Fact]
    public void McpCompanySearchParameters_RequiredFields_ShouldBeValidated()
    {
        // Arrange
        var validParameters = new CompanySearchParameters
        {
            CompanyName = "Apple Inc.",
            Page = 1,
            PageSize = 50
        };

        var invalidParameters = new CompanySearchParameters
        {
            CompanyName = "", // Required field is empty
            Page = 1,
            PageSize = 50
        };

        // Act
        var validResult = ValidateObject(validParameters);
        var invalidResult = ValidateObject(invalidParameters);

        // Assert - MCP parameter validation must enforce required fields
        validResult.Should().BeTrue("Valid company search parameters should pass validation");
        invalidResult.Should().BeFalse("Empty company name should fail validation");
    }

    [Fact]
    public void McpContentSearchParameters_RequiredFields_ShouldBeValidated()
    {
        // Arrange
        var validParameters = new ContentSearchParameters
        {
            SearchText = "artificial intelligence",
            Page = 1,
            PageSize = 50
        };

        var invalidParameters = new ContentSearchParameters
        {
            SearchText = "", // Required field is empty
            Page = 1,
            PageSize = 50
        };

        // Act
        var validResult = ValidateObject(validParameters);
        var invalidResult = ValidateObject(invalidParameters);

        // Assert - MCP content search validation must enforce required fields
        validResult.Should().BeTrue("Valid content search parameters should pass validation");
        invalidResult.Should().BeFalse("Empty search text should fail validation");
    }

    [Fact]
    public void McpDocumentSearchResult_JsonSerialization_ShouldPreserveAllFields()
    {
        // Arrange
        var originalResult = new DocumentSearchResult
        {
            Id = "test-id",
            Title = "Test Document",
            CompanyName = "Test Company",
            FormType = "10-K",
            FilingDate = new DateTime(2024, 1, 15),
            Url = "https://example.com/test",
            ContentPreview = "Test content preview",
            FullContent = "Full test content",
            RelevanceScore = 0.95,
            Highlights = new List<string> { "highlight1", "highlight2" }
        };

        // Act
        var json = JsonSerializer.Serialize(originalResult);
        var deserializedResult = JsonSerializer.Deserialize<DocumentSearchResult>(json);

        // Assert - MCP search results must serialize/deserialize correctly
        deserializedResult.Should().NotBeNull();
        deserializedResult!.Id.Should().Be(originalResult.Id);
        deserializedResult.Title.Should().Be(originalResult.Title);
        deserializedResult.CompanyName.Should().Be(originalResult.CompanyName);
        deserializedResult.FormType.Should().Be(originalResult.FormType);
        deserializedResult.FilingDate.Should().Be(originalResult.FilingDate);
        deserializedResult.Url.Should().Be(originalResult.Url);
        deserializedResult.ContentPreview.Should().Be(originalResult.ContentPreview);
        deserializedResult.FullContent.Should().Be(originalResult.FullContent);
        deserializedResult.RelevanceScore.Should().Be(originalResult.RelevanceScore);
        deserializedResult.Highlights.Should().BeEquivalentTo(originalResult.Highlights);
    }

    [Fact]
    public void McpFormFilterParameters_DefaultBehavior_ShouldBeCorrect()
    {
        // Arrange
        var parameters = new FormFilterParameters
        {
            // FormTypes not specified - should be handled by the tool
            Page = 1,
            PageSize = 50
        };

        // Act & Assert - Form filter parameters should be valid even without form types
        var isValid = ValidateObject(parameters);
        isValid.Should().BeTrue("Form filter parameters should be valid without explicit form types");
        
        parameters.FormTypes.Should().BeNull("FormTypes should be null when not specified");
    }

    [Theory]
    [InlineData(1, 10, 100, 10, false, true)]  // First page
    [InlineData(5, 10, 100, 10, true, true)]   // Middle page
    [InlineData(10, 10, 100, 10, true, false)] // Last page
    [InlineData(1, 10, 5, 1, false, false)]    // Only page
    public void McpPaginatedResult_NavigationProperties_ShouldBeAccurate(
        int page, int pageSize, int totalCount, int expectedTotalPages, 
        bool expectedHasPrevious, bool expectedHasNext)
    {
        // Arrange
        var result = new PaginatedResult<string>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        // Act & Assert - MCP pagination navigation must be mathematically correct
        result.TotalPages.Should().Be(expectedTotalPages);
        result.HasPreviousPage.Should().Be(expectedHasPrevious);
        result.HasNextPage.Should().Be(expectedHasNext);
    }

    [Fact]
    public void McpSearchParameters_DateValidation_ShouldBeLogical()
    {
        // Arrange
        var validDateRange = new CompanySearchParameters
        {
            CompanyName = "Apple Inc.",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            Page = 1,
            PageSize = 50
        };

        // Act
        var isValid = ValidateObject(validDateRange);

        // Assert - Valid date ranges should pass validation
        isValid.Should().BeTrue("Valid date range should pass validation");
        
        // Additional logical validation
        if (validDateRange.StartDate.HasValue && validDateRange.EndDate.HasValue)
        {
            validDateRange.StartDate.Value.Should().BeOnOrBefore(validDateRange.EndDate.Value, 
                "Start date should not be after end date");
        }
    }

    private static bool ValidateObject(object obj)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        return Validator.TryValidateObject(obj, validationContext, validationResults, true);
    }
}