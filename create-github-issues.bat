@echo off
echo Creating GitHub Issues and Milestones...
echo.

pwsh -NoProfile -ExecutionPolicy Bypass -File "create-github-issues.ps1"

echo.
echo Done! Check your GitHub repository for the created issues.
pause
