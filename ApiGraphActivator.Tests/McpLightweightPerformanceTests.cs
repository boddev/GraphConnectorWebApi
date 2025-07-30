using ApiGraphActivator.McpTools;
using FluentAssertions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ApiGraphActivator.Tests;

/// <summary>
/// Lightweight performance tests for MCP protocol compliance
/// These tests verify that MCP operations meet performance requirements
/// </summary>
public class McpLightweightPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public McpLightweightPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void McpToolResponse_Creation_ShouldBeFast()
    {
        // Arrange
        const int iterations = 10000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Create many MCP responses
        for (int i = 0; i < iterations; i++)
        {
            var successResponse = McpToolResponse<string>.Success($"content-{i}");
            var errorResponse = McpToolResponse<string>.Error($"error-{i}");
            
            // Basic verification
            successResponse.IsError.Should().BeFalse();
            errorResponse.IsError.Should().BeTrue();
        }

        stopwatch.Stop();

        // Assert - MCP response creation should be very fast
        var averageTimePerResponse = stopwatch.ElapsedMilliseconds / (double)(iterations * 2);
        
        _output.WriteLine($"MCP Response Creation Performance:");
        _output.WriteLine($"  {iterations * 2} responses created in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average time per response: {averageTimePerResponse:F4}ms");
        
        averageTimePerResponse.Should().BeLessThan(0.1, "MCP response creation should be sub-millisecond");
    }

    [Fact]
    public void McpSerialization_Performance_ShouldBeFast()
    {
        // Arrange
        var largeResult = new PaginatedResult<DocumentSearchResult>
        {
            Items = Enumerable.Range(1, 100).Select(i => new DocumentSearchResult
            {
                Id = $"doc-{i}",
                Title = $"Document {i}",
                CompanyName = "Test Company",
                FormType = "10-K",
                FilingDate = DateTime.UtcNow.AddDays(-i),
                Url = $"https://example.com/doc-{i}",
                ContentPreview = new string('A', 200), // 200 characters
                RelevanceScore = 0.9
            }).ToList(),
            TotalCount = 1000,
            Page = 1,
            PageSize = 100
        };

        var response = McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(largeResult);
        
        const int iterations = 100;
        var stopwatch = Stopwatch.StartNew();

        // Act - Serialize many times
        for (int i = 0; i < iterations; i++)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(response);
            json.Should().NotBeNullOrEmpty();
        }

        stopwatch.Stop();

        // Assert - MCP serialization should be efficient
        var averageTime = stopwatch.ElapsedMilliseconds / (double)iterations;
        
        _output.WriteLine($"MCP Serialization Performance:");
        _output.WriteLine($"  {iterations} serializations in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average time per serialization: {averageTime:F2}ms");
        
        averageTime.Should().BeLessThan(50, "Large MCP response serialization should be under 50ms");
    }

    [Fact]
    public void McpValidation_Performance_ShouldBeFast()
    {
        // Arrange
        var validParameters = new CompanySearchParameters
        {
            CompanyName = "Apple Inc.",
            FormTypes = new List<string> { "10-K", "10-Q" },
            Page = 1,
            PageSize = 50
        };

        var invalidParameters = new CompanySearchParameters
        {
            CompanyName = "", // Invalid
            Page = 0, // Invalid
            PageSize = 2000 // Invalid
        };

        const int iterations = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Validate many parameters
        for (int i = 0; i < iterations; i++)
        {
            var validResult = ValidateObject(validParameters);
            var invalidResult = ValidateObject(invalidParameters);
            
            validResult.Should().BeTrue();
            invalidResult.Should().BeFalse();
        }

        stopwatch.Stop();

        // Assert - MCP validation should be very fast
        var averageTime = stopwatch.ElapsedMilliseconds / (double)(iterations * 2);
        
        _output.WriteLine($"MCP Validation Performance:");
        _output.WriteLine($"  {iterations * 2} validations in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average time per validation: {averageTime:F4}ms");
        
        averageTime.Should().BeLessThan(0.5, "MCP parameter validation should be sub-millisecond");
    }

    [Fact]
    public void McpFormTypeValidation_Performance_ShouldBeFast()
    {
        // Arrange
        var formTypes = new[] { "10-K", "10-Q", "8-K", "INVALID", "10-X", "20-F" };
        const int iterations = 10000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Validate form types many times
        for (int i = 0; i < iterations; i++)
        {
            foreach (var formType in formTypes)
            {
                var isValid = FormTypes.IsValidFormType(formType);
                // Basic verification
                if (formType.StartsWith("10-") || formType.StartsWith("8-"))
                {
                    if (FormTypes.AllFormTypes.Contains(formType))
                    {
                        isValid.Should().BeTrue();
                    }
                }
            }
        }

        stopwatch.Stop();

        // Assert - Form type validation should be very fast
        var averageTime = stopwatch.ElapsedMilliseconds / (double)(iterations * formTypes.Length);
        
        _output.WriteLine($"MCP Form Type Validation Performance:");
        _output.WriteLine($"  {iterations * formTypes.Length} validations in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average time per validation: {averageTime:F6}ms");
        
        averageTime.Should().BeLessThan(0.01, "Form type validation should be sub-10-millisecond");
    }

    [Fact]
    public void McpPaginationCalculation_Performance_ShouldBeFast()
    {
        // Arrange
        const int iterations = 10000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Calculate pagination properties many times
        for (int i = 1; i <= iterations; i++)
        {
            var result = new PaginatedResult<string>
            {
                TotalCount = i * 10,
                Page = i % 10 + 1,
                PageSize = 50
            };

            // Trigger property calculations
            var totalPages = result.TotalPages;
            var hasNext = result.HasNextPage;
            var hasPrevious = result.HasPreviousPage;

            // Basic verification
            totalPages.Should().BeGreaterThan(0);
        }

        stopwatch.Stop();

        // Assert - Pagination calculations should be very fast
        var averageTime = stopwatch.ElapsedMilliseconds / (double)iterations;
        
        _output.WriteLine($"MCP Pagination Calculation Performance:");
        _output.WriteLine($"  {iterations} calculations in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Average time per calculation: {averageTime:F4}ms");
        
        averageTime.Should().BeLessThan(0.01, "Pagination calculations should be microsecond-level");
    }

    [Fact]
    public void McpMemoryUsage_ShouldBeReasonable()
    {
        // Arrange - Force garbage collection to get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Create many MCP objects
        var responses = new List<McpToolResponse<PaginatedResult<DocumentSearchResult>>>();
        
        for (int i = 0; i < 1000; i++)
        {
            var result = new PaginatedResult<DocumentSearchResult>
            {
                Items = Enumerable.Range(1, 10).Select(j => new DocumentSearchResult
                {
                    Id = $"doc-{i}-{j}",
                    Title = $"Document {j}",
                    CompanyName = "Test Company",
                    FormType = "10-K",
                    FilingDate = DateTime.UtcNow,
                    Url = $"https://example.com/doc-{j}",
                    RelevanceScore = 0.9
                }).ToList(),
                TotalCount = 100,
                Page = 1,
                PageSize = 10
            };

            responses.Add(McpToolResponse<PaginatedResult<DocumentSearchResult>>.Success(result));
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert - Memory usage should be reasonable
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseKB = memoryIncrease / 1024.0;
        
        _output.WriteLine($"MCP Memory Usage:");
        _output.WriteLine($"  Created 1000 MCP responses with 10,000 search results");
        _output.WriteLine($"  Memory increase: {memoryIncreaseKB:F2} KB");
        _output.WriteLine($"  Memory per response: {memoryIncreaseKB / 1000.0:F2} KB");
        
        memoryIncreaseKB.Should().BeLessThan(50000, "Memory increase should be under 50MB for 1000 responses");
        
        // Clear references to help with cleanup
        responses.Clear();
    }

    private static bool ValidateObject(object obj)
    {
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(obj);
        return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            obj, validationContext, validationResults, true);
    }
}