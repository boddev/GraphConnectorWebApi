# GitHub Issues Creation Scripts

This folder contains PowerShell scripts to automatically create GitHub issues and milestones from the MCP implementation tasks defined in `github_issues_mcp.md`.

## Prerequisites

1. **GitHub CLI**: Install the GitHub CLI tool
   ```powershell
   winget install GitHub.CLI
   ```

2. **Authentication**: Authenticate with GitHub
   ```powershell
   gh auth login
   ```

3. **Repository Access**: Ensure you have write access to the repository where you want to create issues.

## Scripts

### `create-github-issues.ps1`
Main script that parses the markdown file and creates GitHub issues and milestones.

**Parameters:**
- `-Owner`: GitHub username/organization (default: "boddev")
- `-Repo`: Repository name (default: "GraphConnectorWebApi")
- `-FilePath`: Path to the markdown file (default: "github_issues_mcp.md")
- `-DryRun`: Preview what will be created without actually creating anything
- `-CreateMilestones`: Whether to create milestones (default: true)

### `preview-github-issues.ps1`
Simple wrapper script that runs a dry run to preview what will be created.

## Usage

### 1. Preview First (Recommended)
```powershell
.\preview-github-issues.ps1
```

### 2. Create Issues for Current Repository
```powershell
.\create-github-issues.ps1
```

### 3. Create Issues for Different Repository
```powershell
.\create-github-issues.ps1 -Owner "your-username" -Repo "your-repo"
```

### 4. Dry Run with Custom Parameters
```powershell
.\create-github-issues.ps1 -Owner "your-username" -Repo "your-repo" -DryRun
```

### 5. Skip Milestone Creation
```powershell
.\create-github-issues.ps1 -CreateMilestones:$false
```

## What Gets Created

### Milestones
The script automatically creates milestones for each unique milestone mentioned in the markdown file:
- Phase 1 - Core Infrastructure
- Phase 2 - AI Integration
- Phase 3 - API Development
- Phase 3 - Optimization
- Phase 4 - Testing & QA
- Phase 4 - Frontend Integration
- Phase 5 - Production Deployment

### Issues
The script creates **only task issues** from your markdown file. Epic issues are skipped since GitHub doesn't have native epic support.

**What gets created:**
- **Task Issues**: All issues that don't have `[EPIC]` in the title

**What gets skipped:**
- **Epic Issues**: Issues with titles starting with `[EPIC]` are skipped

Each task issue includes:
- **Title**: Extracted from the markdown
- **Body**: Clean description and acceptance criteria
- **Labels**: Parsed from the Labels field in markdown
- **Milestone**: Associated milestone
- **Epic Reference**: Epic information is preserved in the issue body
- **Estimated Hours**: Added to the issue body

### Epic Information Preservation
While epic issues aren't created, the epic information is preserved:
- Epic names are included in task issue descriptions
- Milestones organize related tasks
- Labels help categorize and filter related tasks

### Labels Applied
The script preserves all labels from the markdown:
- Priority: `P0`, `P1`, `P2`, `P3`
- Type: `epic`, `task`
- Complexity: `complexity-2`, `complexity-3`, `complexity-4`, `complexity-5`
- Category: `infrastructure`, `document-retrieval`, `ai-conversation`, etc.
- Risk: `high-risk`

## Rate Limiting

The script includes a 1-second delay between issue creation to respect GitHub's rate limits.

## Error Handling

- Validates GitHub CLI installation and authentication
- Checks file existence
- Handles API errors gracefully
- Provides detailed output for troubleshooting

## Example Output

```
Starting GitHub Issues creation for boddev/GraphConnectorWebApi
File: github_issues_mcp.md
Dry Run: False

Found 36 total issues in markdown
Will create 27 task issues (skipping 9 epic issues)
Found 7 milestones to create

Creating milestones...
Creating milestone: Phase 1 - Core Infrastructure
✓ Milestone created: Phase 1 - Core Infrastructure

Creating issues...
Creating 27 task issues...
Creating issue: MCP-001 - Implement MCPServerService Core
✓ Issue created: MCP-001 - Implement MCPServerService Core

✓ GitHub Issues creation completed!

Summary:
  Total Issues Created: 27
  Epic Issues Skipped: 9
  Task Issues: 27
  Milestones: 7

Note: Epic issues were skipped since GitHub doesn't have native epic support.
Epic information is preserved in task issue descriptions and milestone organization.
```

## Troubleshooting

1. **Authentication Error**: Run `gh auth login` and follow the prompts
2. **Permission Error**: Ensure you have write access to the repository
3. **Rate Limiting**: The script includes delays, but if you hit limits, wait and retry
4. **Duplicate Issues**: The script doesn't check for existing issues, so running it multiple times will create duplicates

## Notes

- The script processes the exact markdown format from `github_issues_mcp.md`
- **Epic issues are skipped** - GitHub doesn't have native epic support
- Epic information is preserved in task issue descriptions and milestone organization
- All formatting and metadata is preserved from the original markdown
- The script is designed to be idempotent-safe but doesn't check for existing issues
- Use milestones and labels to organize and track related tasks instead of epics
