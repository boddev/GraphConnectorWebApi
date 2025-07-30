using ApiGraphActivator.McpTools;

namespace ApiGraphActivator.Tests.UnitTests.McpTools;

public class McpToolBaseTests
{
    [Fact]
    public void McpToolResponse_Success_ShouldCreateSuccessResponse()
    {
        // Arrange
        var content = "test content";
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var response = McpToolResponse<string>.Success(content, metadata);

        // Assert
        response.Should().NotBeNull();
        response.IsError.Should().BeFalse();
        response.Content.Should().Be(content);
        response.ErrorMessage.Should().BeNull();
        response.Metadata.Should().Equal(metadata);
    }

    [Fact]
    public void McpToolResponse_Error_ShouldCreateErrorResponse()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var response = McpToolResponse<string>.Error(errorMessage);

        // Assert
        response.Should().NotBeNull();
        response.IsError.Should().BeTrue();
        response.ErrorMessage.Should().Be(errorMessage);
        response.Content.Should().BeNull();
        response.Metadata.Should().BeNull();
    }

    [Fact]
    public void PaginationParameters_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var pagination = new PaginationParameters();

        // Assert
        pagination.Page.Should().Be(1);
        pagination.PageSize.Should().Be(50);
        pagination.Skip.Should().Be(0);
    }

    [Fact]
    public void PaginationParameters_Skip_ShouldCalculateCorrectly()
    {
        // Arrange
        var pagination = new PaginationParameters
        {
            Page = 3,
            PageSize = 25
        };

        // Act & Assert
        pagination.Skip.Should().Be(50); // (3-1) * 25
    }

    [Fact]
    public void PaginatedResult_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var result = new PaginatedResult<string>
        {
            Items = items,
            TotalCount = 100,
            Page = 2,
            PageSize = 25
        };

        // Act & Assert
        result.TotalPages.Should().Be(4); // Math.Ceiling(100 / 25)
        result.HasNextPage.Should().BeTrue(); // Page 2 < 4
        result.HasPreviousPage.Should().BeTrue(); // Page 2 > 1
    }

    [Fact]
    public void PaginatedResult_FirstPage_ShouldNotHavePreviousPage()
    {
        // Arrange
        var result = new PaginatedResult<string>
        {
            Items = new List<string>(),
            TotalCount = 100,
            Page = 1,
            PageSize = 25
        };

        // Act & Assert
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void PaginatedResult_LastPage_ShouldNotHaveNextPage()
    {
        // Arrange
        var result = new PaginatedResult<string>
        {
            Items = new List<string>(),
            TotalCount = 100,
            Page = 4,
            PageSize = 25
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void PaginatedResult_EmptyResult_ShouldHandleCorrectly()
    {
        // Arrange
        var result = new PaginatedResult<string>
        {
            Items = new List<string>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 25
        };

        // Act & Assert
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}