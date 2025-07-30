# GitHub Issues Creator for MCP Server Integration
# This script parses the github_issues_mcp.md file and creates GitHub issues and milestones
# using the GitHub CLI

param(
    [Parameter(Mandatory=$false)]
    [string]$Owner = "boddev",
    
    [Parameter(Mandatory=$false)]
    [string]$Repo = "GraphConnectorWebApi",
    
    [Parameter(Mandatory=$false)]
    [string]$FilePath = "github_issues_mcp.md",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateMilestones = $true
)

# Check if GitHub CLI is installed
if (-not (Get-Command "gh" -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) is not installed or not in PATH. Please install it first."
    exit 1
}

# Check if user is authenticated
try {
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Not authenticated with GitHub CLI. Please run 'gh auth login' first."
        exit 1
    }
}
catch {
    Write-Error "Error checking GitHub CLI authentication: $_"
    exit 1
}

# Validate file exists
if (-not (Test-Path $FilePath)) {
    Write-Error "File '$FilePath' not found."
    exit 1
}

Write-Host "Starting GitHub Issues creation for $Owner/$Repo" -ForegroundColor Green
Write-Host "File: $FilePath" -ForegroundColor Yellow
Write-Host "Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host ""

# Read the markdown file
$content = Get-Content $FilePath -Raw

# Function to extract text between patterns
function Get-TextBetween {
    param(
        [string]$Text,
        [string]$Start,
        [string]$End
    )
    
    $startIndex = $Text.IndexOf($Start)
    if ($startIndex -eq -1) { return "" }
    
    $startIndex += $Start.Length
    $endIndex = $Text.IndexOf($End, $startIndex)
    if ($endIndex -eq -1) { $endIndex = $Text.Length }
    
    return $Text.Substring($startIndex, $endIndex - $startIndex).Trim()
}

# Function to parse labels from markdown
function Parse-Labels {
    param([string]$Text)
    
    if ($Text -match '\*\*Labels:\*\*\s*(.+)') {
        $labelsText = $matches[1]
        # Extract labels from backticks
        $labels = [regex]::Matches($labelsText, '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value }
        return $labels
    }
    return @()
}

# Function to parse milestone from markdown
function Parse-Milestone {
    param([string]$Text)
    
    if ($Text -match '\*\*Milestone:\*\*\s*(.+?)(?:\n|$)') {
        return $matches[1].Trim()
    }
    return ""
}

# Function to parse estimated hours
function Parse-EstimatedHours {
    param([string]$Text)
    
    if ($Text -match '\*\*Estimated Hours:\*\*\s*(\d+)') {
        return $matches[1]
    }
    return ""
}

# Function to parse epic name
function Parse-Epic {
    param([string]$Text)
    
    if ($Text -match '\*\*Epic:\*\*\s*(.+)') {
        return $matches[1].Trim()
    }
    return ""
}

# Function to clean markdown for GitHub issue body
function Clean-MarkdownBody {
    param([string]$Text)
    
    # Remove the first line (issue title) and metadata lines
    $lines = $Text -split "`n"
    $cleanLines = @()
    $skipNextLine = $false
    
    foreach ($line in $lines) {
        if ($line -match '^\*\*Labels:\*\*|^\*\*Epic:\*\*|^\*\*Milestone:\*\*|^\*\*Estimated Hours:\*\*') {
            continue
        }
        if ($line -match '^### Issue:') {
            continue
        }
        $cleanLines += $line
    }
    
    return ($cleanLines -join "`n").Trim()
}

# Function to create a milestone
function Create-Milestone {
    param(
        [string]$Title,
        [string]$Description = ""
    )
    
    Write-Host "Creating milestone: $Title" -ForegroundColor Cyan
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would create milestone: $Title" -ForegroundColor Yellow
        return
    }
    
    try {
        $result = gh api repos/$Owner/$Repo/milestones --method POST --field title="$Title" --field description="$Description"
        Write-Host "✓ Milestone created: $Title" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to create milestone '$Title': $_"
    }
}

# Function to create a label
function Create-Label {
    param(
        [string]$Name,
        [string]$Color = "0366d6",
        [string]$Description = ""
    )
    
    Write-Host "Creating label: $Name" -ForegroundColor Cyan
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would create label: $Name" -ForegroundColor Yellow
        return
    }
    
    try {
        $result = gh api repos/$Owner/$Repo/labels --method POST --field name="$Name" --field color="$Color" --field description="$Description" 2>$null
        Write-Host "✓ Label created: $Name" -ForegroundColor Green
    }
    catch {
        # Label might already exist, which is fine
        Write-Host "- Label '$Name' may already exist" -ForegroundColor Yellow
    }
}

# Function to create a GitHub issue
function Create-Issue {
    param(
        [string]$Title,
        [string]$Body,
        [string[]]$Labels = @(),
        [string]$Milestone = "",
        [string]$Epic = "",
        [string]$EstimatedHours = ""
    )
    
    Write-Host "Creating issue: $Title" -ForegroundColor Cyan
    
    # Add estimated hours to body if available
    if ($EstimatedHours) {
        $Body = "**Estimated Hours:** $EstimatedHours`n`n$Body"
    }
    
    # Add epic reference to body if available
    if ($Epic) {
        $Body = "**Epic:** $Epic`n`n$Body"
    }
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would create issue: $Title" -ForegroundColor Yellow
        Write-Host "  Labels: $($Labels -join ', ')" -ForegroundColor Yellow
        Write-Host "  Milestone: $Milestone" -ForegroundColor Yellow
        return
    }
    
    try {
        $issueArgs = @("issue", "create", "--title", $Title, "--body", $Body)
        
        if ($Labels.Count -gt 0) {
            $issueArgs += "--label"
            $issueArgs += ($Labels -join ",")
        }
        
        if ($Milestone) {
            $issueArgs += "--milestone"
            $issueArgs += $Milestone
        }
        
        $result = gh @issueArgs 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Issue created: $Title" -ForegroundColor Green
        } else {
            Write-Warning "Failed to create issue '$Title': $result"
        }
        return $result
    }
    catch {
        Write-Error "Failed to create issue '$Title': $_"
        return $null
    }
}

# Parse the markdown file to extract issues
$issues = @()
$milestones = @()
$allLabels = @()

# Split content by issue sections
$issuePattern = '(?=### Issue:)'
$issueSections = [regex]::Split($content, $issuePattern) | Where-Object { $_ -match '### Issue:' }

foreach ($section in $issueSections) {
    $lines = $section -split "`n"
    $titleLine = $lines | Where-Object { $_ -match '### Issue: (.+)' } | Select-Object -First 1
    
    if ($titleLine -match '### Issue: (.+)') {
        $title = $matches[1].Trim()
        
        # Parse metadata
        $labels = Parse-Labels -Text $section
        $milestone = Parse-Milestone -Text $section
        $estimatedHours = Parse-EstimatedHours -Text $section
        $epic = Parse-Epic -Text $section
        
        # Clean body content
        $body = Clean-MarkdownBody -Text $section
        
        $issue = @{
            Title = $title
            Body = $body
            Labels = $labels
            Milestone = $milestone
            Epic = $epic
            EstimatedHours = $estimatedHours
            IsEpic = $title -match '^\[EPIC\]'
        }
        
        $issues += $issue
        
        # Collect unique milestones
        if ($milestone -and $milestone -notin $milestones) {
            $milestones += $milestone
        }
        
        # Collect unique labels
        foreach ($label in $labels) {
            if ($label -notin $allLabels) {
                $allLabels += $label
            }
        }
    }
}

Write-Host "Found $($issues.Count) total issues in markdown" -ForegroundColor Green
$tasks = $issues | Where-Object { -not $_.IsEpic }
$epics = $issues | Where-Object { $_.IsEpic }
Write-Host "Will create $($tasks.Count) task issues (skipping $($epics.Count) epic issues)" -ForegroundColor Green
Write-Host "Found $($milestones.Count) milestones to create" -ForegroundColor Green
Write-Host "Found $($allLabels.Count) unique labels to create" -ForegroundColor Green
Write-Host ""

# Create labels first
if ($allLabels.Count -gt 0) {
    Write-Host "Creating labels..." -ForegroundColor Blue
    
    # Define label colors based on type
    $labelColors = @{
        "epic" = "8B5CF6"      # Purple
        "task" = "22C55E"      # Green
        "P0" = "DC2626"        # Red
        "P1" = "F59E0B"        # Orange
        "P2" = "3B82F6"        # Blue
        "P3" = "6B7280"        # Gray
        "high-risk" = "EF4444" # Red
        "infrastructure" = "10B981"
        "document-retrieval" = "8B5CF6"
        "ai-conversation" = "F59E0B"
        "copilot-integration" = "3B82F6"
        "api-development" = "06B6D4"
        "performance" = "84CC16"
        "testing" = "F97316"
        "frontend" = "EC4899"
        "deployment" = "6366F1"
    }
    
    foreach ($label in $allLabels) {
        $color = if ($labelColors.ContainsKey($label)) { $labelColors[$label] } else { "0366d6" }
        Create-Label -Name $label -Color $color -Description "Label for MCP project tasks"
    }
    Write-Host ""
}

# Create milestones first if requested
if ($CreateMilestones) {
    Write-Host "Creating milestones..." -ForegroundColor Blue
    foreach ($milestone in $milestones) {
        Create-Milestone -Title $milestone -Description "Milestone for MCP Server Integration project"
    }
    Write-Host ""
}

# Create issues (tasks only, skip epics)
Write-Host "Creating issues..." -ForegroundColor Blue

# Create only regular tasks (skip epics since GitHub doesn't have native epic support)
$tasks = $issues | Where-Object { -not $_.IsEpic }
Write-Host "Creating $($tasks.Count) task issues..." -ForegroundColor Blue
foreach ($task in $tasks) {
    Create-Issue -Title $task.Title -Body $task.Body -Labels $task.Labels -Milestone $task.Milestone -Epic $task.Epic -EstimatedHours $task.EstimatedHours
    Start-Sleep -Seconds 1  # Rate limiting
}

Write-Host ""
Write-Host "✓ GitHub Issues creation completed!" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "This was a dry run. To actually create the issues, run the script without the -DryRun flag:" -ForegroundColor Yellow
    Write-Host "  .\create-github-issues.ps1" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Blue
Write-Host "  Total Issues Created: $($tasks.Count)" -ForegroundColor White
Write-Host "  Epic Issues Skipped: $($epics.Count)" -ForegroundColor Yellow
Write-Host "  Task Issues: $($tasks.Count)" -ForegroundColor White
Write-Host "  Milestones: $($milestones.Count)" -ForegroundColor White
Write-Host "  Labels Created: $($allLabels.Count)" -ForegroundColor White
Write-Host ""
Write-Host "Note: Epic issues were skipped since GitHub doesn't have native epic support." -ForegroundColor Yellow
Write-Host "Epic information is preserved in task issue descriptions and milestone organization." -ForegroundColor Yellow
