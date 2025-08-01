# Simple test to debug SEC API call for Apple
Write-Host "Testing SEC API directly for Apple (AAPL)..." -ForegroundColor Green

$cik = "320193"
$paddedCik = $cik.PadLeft(10, '0')
$url = "https://data.sec.gov/submissions/CIK$paddedCik.json"

Write-Host "Fetching from URL: $url" -ForegroundColor Yellow

try {
    # Set user agent like the application does
    $headers = @{
        'User-Agent' = 'Microsoft/1.0 (test@example.com)'
    }
    
    $response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get
    
    Write-Host "✅ Successfully retrieved SEC data for Apple" -ForegroundColor Green
    Write-Host "Company Name: $($response.name)" -ForegroundColor Cyan
    Write-Host "CIK: $($response.cik)" -ForegroundColor Cyan
    Write-Host "SIC: $($response.sic)" -ForegroundColor Cyan
    
    $recentFilings = $response.filings.recent
    $totalFilings = $recentFilings.accessionNumber.Count
    Write-Host "Total Recent Filings: $totalFilings" -ForegroundColor Cyan
    
    if ($totalFilings -gt 0) {
        Write-Host "`nFirst 5 filings:" -ForegroundColor Yellow
        for ($i = 0; $i -lt [Math]::Min(5, $totalFilings); $i++) {
            $form = $recentFilings.form[$i]
            $date = $recentFilings.reportDate[$i]
            $primaryDoc = $recentFilings.primaryDocument[$i]
            Write-Host "  $($i+1). Form: $form, Date: $date, Doc: $primaryDoc" -ForegroundColor White
        }
        
        # Check date range
        $dates = $recentFilings.reportDate | ForEach-Object { [DateTime]::Parse($_) }
        $latestDate = ($dates | Measure-Object -Maximum).Maximum
        $oldestDate = ($dates | Measure-Object -Minimum).Minimum
        Write-Host "`nDate Range: $oldestDate to $latestDate" -ForegroundColor Cyan
        
        # Check how many are within 3 years
        $cutoffDate = (Get-Date).AddYears(-3)
        $recentCount = ($dates | Where-Object { $_ -gt $cutoffDate }).Count
        Write-Host "Filings within 3 years: $recentCount" -ForegroundColor Green
    } else {
        Write-Host "❌ No recent filings found" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error fetching SEC data: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nSEC API Test Complete!" -ForegroundColor Green
