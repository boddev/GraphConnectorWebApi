# PowerShell script to create company config file
$tickers = @(
    "AMD", "GOOG", "AMZN", "AAPL", "BAC", "COF", "CFG", "FITB", 
    "INTC", "JPM", "MTB", "META", "MSFT", "NVDA", "ORCL", "PNC", 
    "QCOM", "RF", "CRM", "TFC", "USB", "WFC"
)

# Read the SEC company data
$jsonData = Get-Content "company_tickers.json" | ConvertFrom-Json

$companies = @()

foreach ($ticker in $tickers) {
    $found = $false
    foreach ($property in $jsonData.PSObject.Properties) {
        $company = $property.Value
        if ($company.ticker -eq $ticker) {
            $companies += @{
                cik = [int]$company.cik_str
                ticker = $company.ticker
                title = $company.title
            }
            Write-Host "Found: $ticker - $($company.title) (CIK: $($company.cik_str))"
            $found = $true
            break
        }
    }
    if (-not $found) {
        Write-Warning "Company not found for ticker: $ticker"
    }
}

# Create the config object
$config = @{
    lastCrawlDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    companies = $companies
    totalCompanies = $companies.Count
}

# Convert to JSON with proper formatting
$jsonConfig = $config | ConvertTo-Json -Depth 3

# Write to the config file
$configPath = "crawled-companies.json"
$jsonConfig | Out-File -FilePath $configPath -Encoding UTF8

Write-Host "`nConfig file created: $configPath"
Write-Host "Total companies found: $($companies.Count)"
