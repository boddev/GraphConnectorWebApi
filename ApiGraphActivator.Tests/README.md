# MCP Testing Suite

This comprehensive testing suite provides unit tests, integration tests, performance tests, and mock testing for Microsoft 365 Copilot integration for the SEC Edgar Graph Connector Web API.

## Overview

The testing suite is organized into several categories:

- **Unit Tests**: Test individual MCP components in isolation
- **Integration Tests**: Test MCP tools working together end-to-end
- **Performance Tests**: Validate system performance under load
- **Mock Tests**: Test M365 Copilot integration with mock services

## Test Structure

```
ApiGraphActivator.Tests/
├── UnitTests/
│   ├── McpTools/           # Tests for MCP tool components
│   └── Services/           # Tests for service layer
├── IntegrationTests/
│   ├── McpTools/           # End-to-end MCP workflow tests
│   └── Api/                # API integration tests
├── PerformanceTests/       # Performance and load testing
├── Mocks/                  # Mock implementations
├── TestData/               # Test data builders and fixtures
└── TestInfrastructure/     # Test configuration and utilities
```

## Test Coverage

### Unit Tests (72 tests)
- **MCP Tool Base Tests**: Core MCP infrastructure validation
- **Company Search Tool Tests**: Company-based document search functionality
- **Form Filter Tool Tests**: SEC form type filtering and date range searches
- **Content Search Tool Tests**: Full-text content search capabilities
- **Document Search Service Tests**: Service layer business logic

### Integration Tests (21 tests)
- **Document Search Integration**: End-to-end MCP tool workflows
- **Cross-tool Consistency**: Validation of consistent behavior across tools
- **Pagination Integration**: Multi-tool pagination testing
- **Schema Validation**: Input/output schema consistency

### M365 Copilot Integration Tests (8 tests)
- **Authentication Flow**: Mock Microsoft Graph authentication
- **External Connection Management**: Creating and managing Graph connectors
- **Document Indexing**: Indexing SEC documents for Copilot search
- **Search Integration**: Testing search functionality with mock Graph API
- **Error Handling**: Authentication and connection error scenarios

### Performance Tests (8 tests)
- **Response Time Validation**: < 200ms for basic searches, < 1000ms for content search
- **Throughput Testing**: > 10 requests/second capacity
- **Concurrent User Support**: 35+ simultaneous users
- **Memory Usage**: < 50MB memory increase for 100 operations
- **Scalability**: Linear performance scaling with data size

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Categories
```bash
# Unit tests only
dotnet test --filter "FullyQualifiedName~UnitTests"

# Integration tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Performance tests only
dotnet test --filter "FullyQualifiedName~PerformanceTests"

# Exclude performance tests (for faster CI)
dotnet test --filter "FullyQualifiedName!~PerformanceTests"
```

### Generate Test Coverage Report
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Framework Dependencies

- **xUnit**: Primary testing framework for .NET 8
- **Moq**: Mocking framework for external dependencies
- **FluentAssertions**: Readable test assertions
- **AutoFixture**: Test data generation
- **Microsoft.AspNetCore.Mvc.Testing**: API integration testing

## Mock Services

### MockCrawlStorageService
Provides in-memory storage simulation for testing document search operations without external dependencies.

**Features:**
- Document storage and retrieval
- Company-based search simulation
- Form type and date filtering
- Performance metrics tracking

### MockGraphService
Simulates Microsoft Graph API operations for M365 Copilot integration testing.

**Features:**
- Authentication simulation
- External connection management
- Document indexing simulation
- Search result generation
- Error scenario testing

## Test Data Management

### TestDataBuilder
Provides builders for creating consistent test data across all test scenarios.

**Key Methods:**
- `CreateDocumentInfo()`: Generate test SEC documents
- `CreateCompanySearchParameters()`: Build search parameters
- `CreateTestDocuments()`: Generate document collections
- `CreatePaginatedResult()`: Build paginated response data

### TestDataManager
Manages different test scenarios and datasets for comprehensive testing.

**Scenarios:**
- **Comprehensive**: Full dataset with multiple companies and form types
- **Performance**: Large dataset (10,000+ documents) for load testing
- **Apple Only**: Focused dataset for specific company testing
- **Quarterly Reports**: Specific to 10-Q forms
- **Recent Filings**: Last 90 days of documents

## Performance Benchmarks

### Target Performance Metrics

| Operation | Target Response Time | Throughput |
|-----------|---------------------|------------|
| Company Search | < 200ms | 10+ req/sec |
| Form Filter | < 200ms | 10+ req/sec |
| Content Search | < 1000ms | 5+ req/sec |
| Concurrent Users | < 1000ms under load | 35+ users |

### Memory Usage Limits
- Maximum 50MB memory increase per 100 operations
- Garbage collection should not cause significant delays
- No memory leaks over extended operation periods

## Continuous Integration

The test suite is designed to run in CI/CD pipelines with the following considerations:

- **Fast Execution**: Unit and integration tests complete in < 2 minutes
- **Deterministic Results**: All tests use fixed seeds for reproducible outcomes
- **Environment Independent**: No external service dependencies
- **Parallel Execution**: Tests are isolated and thread-safe

## Test Configuration

### Environment Variables
No environment variables required for testing - all dependencies are mocked.

### Test Settings
- Default page size: 50 items
- Test timeout: 30 seconds for performance tests
- Memory threshold: 50MB
- Concurrent user simulation: 35 users

## Troubleshooting

### Common Issues

1. **Test Timeouts**: Performance tests may timeout on slower systems
   - Solution: Increase timeout values or skip performance tests in CI

2. **Memory Test Failures**: Garbage collection timing can affect memory tests
   - Solution: Run tests individually or adjust memory thresholds

3. **Flaky Integration Tests**: Concurrent execution may cause timing issues
   - Solution: Tests are designed to be deterministic, check for external interference

### Debug Mode
Enable verbose logging by setting test logger to `Debug` level:
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Contributing

When adding new tests:

1. Follow the existing test structure and naming conventions
2. Use appropriate test categories (Unit/Integration/Performance)
3. Include both positive and negative test scenarios
4. Mock external dependencies appropriately
5. Ensure tests are deterministic and repeatable
6. Add performance tests for new functionality

## Test Results Summary

- **Total Tests**: 109
- **Test Categories**: 4 (Unit, Integration, Performance, Copilot)
- **Code Coverage**: Targets 90%+ for MCP components
- **Execution Time**: < 2 minutes (excluding performance tests)
- **Success Rate**: 100% (all tests passing)

The testing suite provides comprehensive coverage of the MCP (Model Context Protocol) implementation, ensuring reliability, performance, and correct integration with Microsoft 365 Copilot services.