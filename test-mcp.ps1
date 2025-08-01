# PowerShell script to test MCP functionality

Write-Host "Testing MCP Server Functionality" -ForegroundColor Green

# Test 1: List available tools
Write-Host "`n1. Testing tools/list endpoint..." -ForegroundColor Yellow
$toolsListRequest = @{
    jsonrpc = "2.0"
    id = "1"
    method = "tools/list"
    params = @{}
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $toolsListRequest
    Write-Host "✅ Tools list successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Tools list failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Search documents
Write-Host "`n2. Testing search_documents tool..." -ForegroundColor Yellow
$searchRequest = @{
    jsonrpc = "2.0"
    id = "2"
    method = "tools/call"
    params = @{
        name = "search_documents"
        arguments = @{
            query = "what was the revenue for Apple during the last quarter"
            company = "AAPL"
        }
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $searchRequest
    Write-Host "✅ Search documents successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Search documents failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Get crawled companies
Write-Host "`n3. Testing get_crawled_companies tool..." -ForegroundColor Yellow
$companiesRequest = @{
    jsonrpc = "2.0"
    id = "3"
    method = "tools/call"
    params = @{
        name = "get_crawled_companies"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $companiesRequest
    Write-Host "✅ Get crawled companies successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Get crawled companies failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Get status
Write-Host "`n4. Testing get_crawl_status tool..." -ForegroundColor Yellow
$statusRequest = @{
    jsonrpc = "2.0"
    id = "4"
    method = "tools/call"
    params = @{
        name = "get_crawl_status"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $statusRequest
    Write-Host "✅ Get crawl status successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Get crawl status failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Get last crawl info
Write-Host "`n5. Testing get_last_crawl_info tool..." -ForegroundColor Yellow
$lastCrawlRequest = @{
    jsonrpc = "2.0"
    id = "5"
    method = "tools/call"
    params = @{
        name = "get_last_crawl_info"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $lastCrawlRequest
    Write-Host "✅ Get last crawl info successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Get last crawl info failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Get document content (if any documents exist)
Write-Host "`n6. Testing get_document_content tool..." -ForegroundColor Yellow
$contentRequest = @{
    jsonrpc = "2.0"
    id = "6"
    method = "tools/call"
    params = @{
        name = "get_document_content"
        arguments = @{
            document_id = "test_doc"
            url = "https://www.sec.gov/Archives/edgar/data/320193/000032019325000007/aapl-20241228.htm"
        }
    }
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5236/mcp" -Method Post -ContentType "application/json" -Body $contentRequest
    Write-Host "✅ Get document content successful:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Get document content failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nMCP Testing Complete!" -ForegroundColor Green
