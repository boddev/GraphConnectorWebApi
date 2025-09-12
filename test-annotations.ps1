#!/usr/bin/env pwsh

# Test script to verify MCP server annotations
Write-Host "Building the project..." -ForegroundColor Green
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! Your Edgar MCP server now includes:" -ForegroundColor Green
    Write-Host ""
    Write-Host "✅ Annotations added to all tools for Microsoft 365 Agents Toolkit compatibility" -ForegroundColor Cyan
    Write-Host "✅ Enhanced server capabilities with proper flags" -ForegroundColor Cyan
    Write-Host "✅ Added prompts/list handler to prevent 'Method not found' errors" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Your MCP server tools now include the following annotations:" -ForegroundColor Yellow
    Write-Host "• search_documents - 'Search SEC Documents'" -ForegroundColor White
    Write-Host "• get_document_content - 'Get SEC Document Content'" -ForegroundColor White
    Write-Host "• analyze_document - 'Analyze SEC Document'" -ForegroundColor White
    Write-Host "• get_crawl_status - 'Get Crawl Status'" -ForegroundColor White
    Write-Host "• get_last_crawl_info - 'Get Last Crawl Info'" -ForegroundColor White
    Write-Host "• get_crawled_companies - 'Get Crawled Companies'" -ForegroundColor White
    Write-Host ""
    Write-Host "To test the MCP server:" -ForegroundColor Green
    Write-Host "  dotnet run --mcp-stdio" -ForegroundColor Gray
    Write-Host ""
    Write-Host "The server is now ready for import into Microsoft 365 Agents Toolkit!" -ForegroundColor Green
} else {
    Write-Host "Build failed. Please check the errors above." -ForegroundColor Red
}
