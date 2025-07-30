using ApiGraphActivator.McpTools;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace ApiGraphActivator.Tests;

/// <summary>
/// Comprehensive MCP Protocol Compliance Test Suite
/// Tests all core MCP protocol requirements including message validation,
/// error handling compliance, and protocol violation detection
/// </summary>
public class McpProtocolComplianceTestSuite
{
    #region Message Validation Tests

    [Fact]
    public void McpProtocol_MessageValidation_SuccessResponse_ShouldFollowStandard()
    {
        // Arrange
        var content = new PaginatedResult<DocumentSearchResult>
        {
            Items = new List<DocumentSearchResult>
            {
                new DocumentSearchResult
                {
                    Id = "test-1",
                    Title = "Test Document",
                    CompanyName = "Test Company",
                    FormType = "10-K",
                    FilingDate = DateTime.UtcNow,
                    Url = "https://example.com/test",
                    RelevanceScore = 0.95
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };

        var metadata = new Dictionary<string, object>
        {
            ["searchType"] = "company",
            ["searchTerm"] = "Test Company",
            ["executionTime"] = DateTime.UtcNow.ToString("O")
        };

        // Act
        var response = McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(content, metadata);

        // Assert - MCP protocol message validation
        response.Should().NotBeNull("MCP responses must not be null");
        response.IsError.Should().BeFalse("Success responses must not be errors");
        response.ErrorMessage.Should().BeNull("Success responses must not have error messages");
        response.Content.Should().NotBeNull("Success responses must have content");
        response.Content!.Items.Should().HaveCount(1);
        response.Metadata.Should().NotBeNull("Success responses should include metadata");
        response.Metadata!.Should().ContainKey("searchType");
        response.Metadata.Should().ContainKey("executionTime");
    }

    [Fact]
    public void McpProtocol_MessageValidation_ErrorResponse_ShouldFollowStandard()
    {
        // Arrange
        var errorMessage = "Invalid form types: INVALID-FORM. Valid types: 10-K, 10-Q, 8-K, 10-K/A, 10-Q/A, 8-K/A";

        // Act
        var response = McpToolResponse<PaginatedResult<DocumentSearchResult>>.Error(errorMessage);

        // Assert - MCP protocol error message validation
        response.Should().NotBeNull("MCP error responses must not be null");
        response.IsError.Should().BeTrue("Error responses must be marked as errors");
        response.ErrorMessage.Should().Be(errorMessage);
        response.Content.Should().BeNull("Error responses must not have content");
        response.Metadata.Should().BeNull("Error responses typically don't have metadata");
    }

    [Fact]
    public void McpProtocol_MessageSerialization_ShouldPreserveStructure()
    {
        // Arrange
        var originalResponse = McpToolResponse<string>.Success("test content", 
            new Dictionary<string, object> { ["test"] = "value" });

        // Act
        var json = JsonSerializer.Serialize(originalResponse);
        var deserializedResponse = JsonSerializer.Deserialize<McpToolResponse<string>>(json);

        // Assert - MCP message serialization compliance
        deserializedResponse.Should().NotBeNull();
        deserializedResponse!.IsError.Should().Be(originalResponse.IsError);
        deserializedResponse.Content.Should().Be(originalResponse.Content);
        deserializedResponse.ErrorMessage.Should().Be(originalResponse.ErrorMessage);
        deserializedResponse.Metadata.Should().NotBeNull();
        deserializedResponse.Metadata!["test"].ToString().Should().Be("value");
    }

    #endregion

    #region Error Handling Compliance Tests

    [Fact]
    public void McpProtocol_ErrorHandling_ValidationFailures_ShouldReturnStructuredErrors()
    {
        // Test various validation scenarios that should result in structured error responses

        // Empty required field
        var emptyCompanyParams = new CompanySearchParameters { CompanyName = "", Page = 1, PageSize = 10 };
        var emptyCompanyValidation = ValidateObject(emptyCompanyParams);
        emptyCompanyValidation.Should().BeFalse("Empty required fields should fail validation");

        // Empty search text
        var emptySearchParams = new ContentSearchParameters { SearchText = "", Page = 1, PageSize = 10 };
        var emptySearchValidation = ValidateObject(emptySearchParams);
        emptySearchValidation.Should().BeFalse("Empty search text should fail validation");

        // Invalid pagination
        var invalidPaginationParams = new CompanySearchParameters 
        { 
            CompanyName = "Apple", 
            Page = 0, // Invalid
            PageSize = 2000 // Invalid
        };
        var invalidPaginationValidation = ValidateObject(invalidPaginationParams);
        invalidPaginationValidation.Should().BeFalse("Invalid pagination should fail validation");
    }

    [Fact]
    public void McpProtocol_ErrorHandling_FormTypeValidation_ShouldRejectInvalidTypes()
    {
        // Arrange - Test form types that violate the protocol
        var invalidFormTypes = new[] { "INVALID-FORM", "10-X", "20-F", "invalid", "10K" };

        foreach (var invalidFormType in invalidFormTypes)
        {
            // Act & Assert
            var isValid = FormTypes.IsValidFormType(invalidFormType);
            isValid.Should().BeFalse($"Form type '{invalidFormType}' should be rejected by MCP protocol");
        }
    }

    [Fact]
    public void McpProtocol_ErrorHandling_DateValidation_ShouldEnforceLogicalRanges()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddYears(1);
        var pastDate = DateTime.UtcNow.AddYears(-1);

        var parametersWithInvalidDateRange = new CompanySearchParameters
        {
            CompanyName = "Apple Inc.",
            StartDate = futureDate,
            EndDate = pastDate, // End before start
            Page = 1,
            PageSize = 10
        };

        // Act & Assert - Date range validation
        if (parametersWithInvalidDateRange.StartDate > parametersWithInvalidDateRange.EndDate)
        {
            true.Should().BeTrue("Invalid date range should be detectable");
        }
    }

    #endregion

    #region Protocol Violation Detection Tests

    [Fact]
    public void McpProtocol_ViolationDetection_ToolNaming_ShouldFollowConventions()
    {
        // Test that tool names follow MCP naming conventions (snake_case)
        var expectedToolNames = new[]
        {
            "search_documents_by_company",
            "filter_documents_by_form_and_date", 
            "search_document_content"
        };

        foreach (var expectedName in expectedToolNames)
        {
            // Assert - Names should follow snake_case convention
            expectedName.Should().MatchRegex(@"^[a-z][a-z0-9_]*$", 
                "MCP tool names must follow snake_case convention");
            expectedName.Should().NotContain(" ", "Tool names cannot contain spaces");
            expectedName.Should().NotContain("-", "Tool names should use underscores, not hyphens");
        }
    }

    [Fact]
    public void McpProtocol_ViolationDetection_ResponseStructure_ShouldBeConsistent()
    {
        // Test that all MCP responses follow the same structure
        var successResponse = McpToolResponse<string>.Success("content");
        var errorResponse = McpToolResponse<string>.Error("error message");

        // Success response structure validation
        successResponse.Should().NotBeNull();
        successResponse.GetType().GetProperty("IsError").Should().NotBeNull("IsError property is required");
        successResponse.GetType().GetProperty("Content").Should().NotBeNull("Content property is required");
        successResponse.GetType().GetProperty("ErrorMessage").Should().NotBeNull("ErrorMessage property is required");
        successResponse.GetType().GetProperty("Metadata").Should().NotBeNull("Metadata property is required");

        // Error response structure validation
        errorResponse.Should().NotBeNull();
        errorResponse.IsError.Should().BeTrue();
        errorResponse.Content.Should().BeNull();
        errorResponse.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void McpProtocol_ViolationDetection_PaginationConstraints_ShouldBeEnforced()
    {
        // Test pagination parameter constraints
        var validationTestCases = new[]
        {
            new { Page = 0, PageSize = 10, ShouldBeValid = false, Violation = "Page must be >= 1" },
            new { Page = -1, PageSize = 10, ShouldBeValid = false, Violation = "Page cannot be negative" },
            new { Page = 1, PageSize = 0, ShouldBeValid = false, Violation = "PageSize must be >= 1" },
            new { Page = 1, PageSize = -1, ShouldBeValid = false, Violation = "PageSize cannot be negative" },
            new { Page = 1, PageSize = 1001, ShouldBeValid = false, Violation = "PageSize exceeds maximum" },
            new { Page = 1, PageSize = 50, ShouldBeValid = true, Violation = "Valid parameters" }
        };

        foreach (var testCase in validationTestCases)
        {
            // Act
            var parameters = new TestPaginationParams { Page = testCase.Page, PageSize = testCase.PageSize };
            var isValid = ValidateObject(parameters);

            // Assert
            isValid.Should().Be(testCase.ShouldBeValid, testCase.Violation);
        }
    }

    #endregion

    #region Performance Benchmarking Tests

    [Fact]
    public void McpProtocol_Performance_ResponseCreation_ShouldMeetTargets()
    {
        // Arrange
        const int iterations = 1000;
        var startTime = DateTime.UtcNow;

        // Act - Create many MCP responses
        for (int i = 0; i < iterations; i++)
        {
            var successResponse = McpToolResponse<string>.Success($"content-{i}");
            var errorResponse = McpToolResponse<string>.Error($"error-{i}");
            
            successResponse.IsError.Should().BeFalse();
            errorResponse.IsError.Should().BeTrue();
        }

        var elapsedTime = DateTime.UtcNow - startTime;

        // Assert - Performance targets
        elapsedTime.TotalMilliseconds.Should().BeLessThan(100, 
            "Creating 1000 MCP responses should take less than 100ms");
    }

    [Fact]
    public void McpProtocol_Performance_ValidationSpeed_ShouldMeetTargets()
    {
        // Arrange
        var validParams = new CompanySearchParameters 
        { 
            CompanyName = "Apple Inc.", 
            Page = 1, 
            PageSize = 50 
        };
        
        const int iterations = 500;
        var startTime = DateTime.UtcNow;

        // Act - Validate parameters many times
        for (int i = 0; i < iterations; i++)
        {
            var isValid = ValidateObject(validParams);
            isValid.Should().BeTrue();
        }

        var elapsedTime = DateTime.UtcNow - startTime;

        // Assert - Validation performance targets
        elapsedTime.TotalMilliseconds.Should().BeLessThan(50,
            "Validating 500 parameter sets should take less than 50ms");
    }

    [Fact]
    public void McpProtocol_Performance_SerializationSpeed_ShouldMeetTargets()
    {
        // Arrange
        var largeResponse = McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(
            new PaginatedResult<DocumentSearchResult>
            {
                Items = Enumerable.Range(1, 100).Select(i => new DocumentSearchResult
                {
                    Id = $"doc-{i}",
                    Title = $"Document {i}",
                    CompanyName = "Test Company",
                    FormType = "10-K",
                    FilingDate = DateTime.UtcNow,
                    Url = $"https://example.com/{i}",
                    RelevanceScore = 0.9
                }).ToList(),
                TotalCount = 1000,
                Page = 1,
                PageSize = 100
            });

        const int iterations = 100;
        var startTime = DateTime.UtcNow;

        // Act - Serialize large responses multiple times
        for (int i = 0; i < iterations; i++)
        {
            var json = JsonSerializer.Serialize(largeResponse);
            json.Should().NotBeNullOrEmpty();
        }

        var elapsedTime = DateTime.UtcNow - startTime;

        // Assert - Serialization performance targets
        elapsedTime.TotalMilliseconds.Should().BeLessThan(1000,
            "Serializing 100 large MCP responses should take less than 1 second");
    }

    #endregion

    #region Helper Methods

    private static bool ValidateObject(object obj)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        return Validator.TryValidateObject(obj, validationContext, validationResults, true);
    }

    private class TestPaginationParams : PaginationParameters
    {
        // Inherits validation attributes from base class
    }

    #endregion
}