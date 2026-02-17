#!/bin/bash
# =============================================================================
# setup-github-secrets.sh
#
# Securely pushes all required secrets to your GitHub repository using the
# GitHub CLI (gh). Secrets are read interactively from the terminal so they
# never appear in shell history or process listings.
#
# Prerequisites:
#   - GitHub CLI (gh) installed and authenticated: gh auth login
#   - Repository access: push rights to boddev/GraphConnectorWebApi
#
# Usage:
#   chmod +x setup-github-secrets.sh
#   ./setup-github-secrets.sh
# =============================================================================

set -euo pipefail

REPO="boddev/GraphConnectorWebApi"

echo "=============================================="
echo " GitHub Secrets Setup for $REPO"
echo "=============================================="
echo ""
echo "This script will securely set the required GitHub Actions secrets."
echo "Each value is read from stdin and piped directly to 'gh secret set',"
echo "so nothing is stored in shell history or environment variables."
echo ""
echo "Press Ctrl+C at any time to cancel."
echo ""

# Helper function: prompt for a secret and push it to GitHub
set_secret() {
    local secret_name="$1"
    local description="$2"
    local default_value="${3:-}"

    echo "----------------------------------------------"
    echo "Secret: $secret_name"
    echo "  $description"

    if [ -n "$default_value" ]; then
        echo "  Default: $default_value"
        read -rp "  Value (press Enter for default): " value
        value="${value:-$default_value}"
    else
        read -rsp "  Value: " value
        echo ""
    fi

    if [ -z "$value" ]; then
        echo "  [SKIPPED] No value provided."
        return
    fi

    echo "$value" | gh secret set "$secret_name" --repo "$REPO"
    echo "  [OK] $secret_name set successfully."
}

# Helper for non-sensitive values (echoes input)
set_secret_visible() {
    local secret_name="$1"
    local description="$2"
    local default_value="${3:-}"

    echo "----------------------------------------------"
    echo "Secret: $secret_name"
    echo "  $description"

    if [ -n "$default_value" ]; then
        echo "  Default: $default_value"
        read -rp "  Value (press Enter for default): " value
        value="${value:-$default_value}"
    else
        read -rp "  Value: " value
    fi

    if [ -z "$value" ]; then
        echo "  [SKIPPED] No value provided."
        return
    fi

    echo "$value" | gh secret set "$secret_name" --repo "$REPO"
    echo "  [OK] $secret_name set successfully."
}

echo ""
echo "=== Azure Resource Group ==="
echo ""
set_secret_visible "AZURE_RESOURCE_GROUP" \
    "The Azure resource group containing the edgar-gc-mcp app service"

echo ""
echo "=== Azure AD / Entra ID Credentials ==="
echo ""
set_secret_visible "AZUREAD_CLIENT_ID" \
    "Azure AD Application (client) ID" \
    "38a2a06a-2d52-4e19-8525-c984f1bb6df0"

set_secret "AZUREAD_CLIENT_SECRET" \
    "Azure AD client secret (will be hidden)"

set_secret_visible "AZUREAD_TENANT_ID" \
    "Azure AD tenant ID" \
    "5174ceb7-3102-4916-9c26-eb94f327f56d"

echo ""
echo "=== Storage Configuration ==="
echo ""
set_secret "TABLE_STORAGE" \
    "Azure Table Storage connection string (will be hidden)"

set_secret "BLOB_CONTAINER_NAME" \
    "Azure Blob Storage container name (will be hidden)"

echo ""
echo "=== Application Insights ==="
echo ""
set_secret "APPLICATIONINSIGHTS_CONNECTION_STRING" \
    "Application Insights connection string (will be hidden)"

echo ""
echo "=== Application Settings ==="
echo ""
set_secret_visible "EMAIL_ADDRESS" \
    "Email address used for SEC EDGAR API User-Agent" \
    "bodonnell@microsoft.com"

echo ""
echo "=============================================="
echo " All secrets have been configured!"
echo "=============================================="
echo ""
echo "You can verify them (names only) with:"
echo "  gh secret list --repo $REPO"
echo ""
echo "Trigger a deployment with:"
echo "  gh workflow run main_edgar-gc-mcp.yml --repo $REPO"
echo ""
