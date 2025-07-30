# Cleanup script to delete all GitHub artifacts created by the MCP issues script
# This script deletes milestones, labels, and any issues that were created

param(
    [Parameter(Mandatory=$false)]
    [string]$Owner = "boddev",
    
    [Parameter(Mandatory=$false)]
    [string]$Repo = "GraphConnectorWebApi",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

Write-Host "Starting cleanup for $Owner/$Repo" -ForegroundColor Red
Write-Host "Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host ""

# Function to delete a milestone
function Delete-Milestone {
    param([int]$Number)
    
    Write-Host "Deleting milestone #$Number" -ForegroundColor Red
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would delete milestone #$Number" -ForegroundColor Yellow
        return
    }
    
    try {
        gh api repos/$Owner/$Repo/milestones/$Number --method DELETE
        Write-Host "✓ Milestone #$Number deleted" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to delete milestone #$Number`: $($_.Exception.Message)"
    }
}

# Function to delete a label
function Delete-Label {
    param([string]$Name)
    
    Write-Host "Deleting label: $Name" -ForegroundColor Red
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would delete label: $Name" -ForegroundColor Yellow
        return
    }
    
    try {
        gh api repos/$Owner/$Repo/labels/$Name --method DELETE
        Write-Host "✓ Label '$Name' deleted" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to delete label '$Name'`: $($_.Exception.Message)"
    }
}

# Function to delete an issue
function Delete-Issue {
    param([int]$Number)
    
    Write-Host "Deleting issue #$Number" -ForegroundColor Red
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would delete issue #$Number" -ForegroundColor Yellow
        return
    }
    
    try {
        # GitHub doesn't allow deleting issues via API, so we'll close them instead
        gh issue close $Number --reason "not planned"
        Write-Host "✓ Issue #$Number closed" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to close issue #$Number`: $($_.Exception.Message)"
    }
}

# Get and delete all issues
Write-Host "Checking for issues to delete..." -ForegroundColor Blue
try {
    $issues = gh api repos/$Owner/$Repo/issues --paginate | ConvertFrom-Json
    if ($issues.Count -gt 0) {
        Write-Host "Found $($issues.Count) issues to close" -ForegroundColor Yellow
        foreach ($issue in $issues) {
            Delete-Issue -Number $issue.number
        }
    } else {
        Write-Host "No issues found" -ForegroundColor Green
    }
}
catch {
    Write-Host "No issues to delete or error occurred: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host ""

# Get and delete all milestones
Write-Host "Checking for milestones to delete..." -ForegroundColor Blue
try {
    $milestones = gh api repos/$Owner/$Repo/milestones | ConvertFrom-Json
    if ($milestones.Count -gt 0) {
        Write-Host "Found $($milestones.Count) milestones to delete" -ForegroundColor Yellow
        foreach ($milestone in $milestones) {
            Delete-Milestone -Number $milestone.number
        }
    } else {
        Write-Host "No milestones found" -ForegroundColor Green
    }
}
catch {
    Write-Host "No milestones to delete or error occurred: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host ""

# Get and delete MCP-related labels
Write-Host "Checking for labels to delete..." -ForegroundColor Blue

# List of labels that were created by our script
$mcpLabels = @(
    "epic", "task", "P0", "P1", "P2", "P3", "infrastructure", "complexity-5", 
    "complexity-3", "complexity-4", "complexity-2", "data-models", "session-management",
    "document-retrieval", "vector-search", "high-risk", "document-context", 
    "ai-conversation", "conversation-management", "citations", "openai-integration",
    "copilot-integration", "copilot-api", "graph-integration", "health-monitoring",
    "api-development", "api-endpoints", "documentation", "security", "performance",
    "caching", "database-optimization", "background-processing", "testing",
    "unit-testing", "integration-testing", "performance-testing", "frontend",
    "chat-interface", "search-interface", "analytics", "deployment", 
    "production-config", "monitoring", "backup-recovery"
)

foreach ($label in $mcpLabels) {
    try {
        # Check if label exists first
        $labelExists = gh api repos/$Owner/$Repo/labels/$label 2>$null
        if ($labelExists) {
            Delete-Label -Name $label
        }
    }
    catch {
        # Label doesn't exist, which is fine
        Write-Host "- Label '$label' not found (already deleted or never created)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "✓ Cleanup completed!" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "This was a dry run. To actually delete everything, run:" -ForegroundColor Yellow
    Write-Host "  .\cleanup-github-artifacts.ps1" -ForegroundColor Yellow
}
