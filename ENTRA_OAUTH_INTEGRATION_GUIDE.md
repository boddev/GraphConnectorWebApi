# Microsoft Entra ID (Azure AD) OAuth Integration Guide

This guide walks you through integrating the SEC Edgar Graph Connector MCP Server with Microsoft Entra ID for OAuth 2.0 authentication. After completing this guide, all API and MCP endpoints will require a valid JWT bearer token issued by your Entra tenant.

---

## Architecture Overview

```
┌──────────────────┐     ┌───────────────────┐     ┌─────────────────────┐
│  MCP Client /    │────▶│  Entra ID         │────▶│  SEC Edgar MCP      │
│  Swagger UI /    │     │  Token Service     │     │  Server (API)       │
│  Custom App      │     │                   │     │                     │
└──────────────────┘     └───────────────────┘     └─────────────────────┘
        │                         │                          │
        │  1. Request token       │                          │
        │  (OAuth 2.0 flow)       │                          │
        │◀─ 2. JWT access token ──│                          │
        │                         │                          │
        │  3. Call API with       │                          │
        │     Bearer token ───────│──────────────────────────▶│
        │                         │                          │  4. Validate JWT
        │                         │                          │     (issuer, audience,
        │                         │                          │      scopes)
```

## Prerequisites

- A Microsoft Entra ID tenant (Azure AD) with admin access
- The SEC Edgar Graph Connector MCP Server deployed and running
- Azure CLI installed (optional, for scripting)

---

## Step 1: Create the Server/API App Registration

This app registration represents the MCP Server itself — it validates incoming tokens and defines the API scopes.

### 1.1 Register the application

1. Go to the [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `SEC-Edgar-MCP-Server`
   - **Supported account types**: *Accounts in this organizational directory only* (single tenant)
   - **Redirect URI**: Leave blank
4. Click **Register**
5. Note the following values from the **Overview** page:
   - **Application (client) ID** → this is your `AzureAd:ClientId`
   - **Directory (tenant) ID** → this is your `AzureAd:TenantId`

### 1.2 Create a client secret

1. Go to **Certificates & secrets** → **New client secret**
2. Set a description (e.g., `MCP Server Secret`) and expiration
3. Click **Add**
4. **Copy the secret Value immediately** (it won't be shown again) → this is your `AzureAd:ClientSecret`

### 1.3 Expose an API (define scopes)

This is critical — it tells Entra ID what permissions your API offers.

1. Go to **Expose an API**
2. Click **Set** next to **Application ID URI** and accept the default (`api://<client-id>`) or set a custom one
3. Click **Add a scope** and create **two scopes**:

   **Scope 1: Mcp.Read**
   - **Scope name**: `Mcp.Read`
   - **Who can consent**: Admins and users
   - **Admin consent display name**: `Read SEC documents via MCP`
   - **Admin consent description**: `Allows the application to search and read SEC documents through the MCP server`
   - **User consent display name**: `Read SEC documents`
   - **User consent description**: `Allows searching and reading SEC documents`
   - **State**: Enabled

   **Scope 2: Mcp.ReadWrite**
   - **Scope name**: `Mcp.ReadWrite`
   - **Who can consent**: Admins only
   - **Admin consent display name**: `Read and write SEC documents via MCP`
   - **Admin consent description**: `Allows the application to search, read, crawl, and manage connections through the MCP server`
   - **User consent display name**: `Read and manage SEC documents`
   - **User consent description**: `Allows searching, reading, crawling, and managing SEC document connections`
   - **State**: Enabled

### 1.4 Configure the existing Microsoft Graph permissions

The server app also needs Graph permissions for the connector functionality. Under **API permissions**:

1. Click **Add a permission** → **Microsoft Graph** → **Application permissions**
2. Add:
   - `ExternalConnection.ReadWrite.OwnedBy`
   - `ExternalItem.ReadWrite.OwnedBy`
3. Click **Grant admin consent for [your org]**

---

## Step 2: Create the Client App Registration

This app registration is for tools, Swagger UI, MCP clients, or other applications that call your API.

### 2.1 Register the application

1. Go to **App registrations** → **New registration**
2. Configure:
   - **Name**: `SEC-Edgar-MCP-Client`
   - **Supported account types**: *Accounts in this organizational directory only*
   - **Redirect URI**:
     - **Platform**: Single-page application (SPA)
     - **URI**: `https://localhost:7189/swagger/oauth2-redirect.html` (for Swagger UI)
     - Add additional URIs for other clients (e.g., `http://localhost:3000` for the React frontend)
3. Click **Register**
4. Note the **Application (client) ID** → this is your `SwaggerUI:ClientId`

### 2.2 Configure API permissions

1. Go to **API permissions** → **Add a permission**
2. Select **My APIs** → select `SEC-Edgar-MCP-Server`
3. Select **Delegated permissions**
4. Check both:
   - `Mcp.Read`
   - `Mcp.ReadWrite`
5. Click **Add permissions**
6. Click **Grant admin consent for [your org]** (or let users consent individually for `Mcp.Read`)

### 2.3 Enable PKCE (already configured)

The client app uses Authorization Code flow with PKCE by default when registered as a SPA. No client secret is needed for the client app.

---

## Step 3: Configure the MCP Server

### 3.1 Set configuration values

Update your server's configuration with the values from Step 1. You can use any of these methods:

**Option A: User Secrets (Development)**

```powershell
cd ApiGraphActivator
dotnet user-secrets set "AzureAd:Instance" "https://login.microsoftonline.com/"
dotnet user-secrets set "AzureAd:TenantId" "<your-tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<server-app-client-id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<server-app-client-secret>"
dotnet user-secrets set "AzureAd:Audience" "api://<server-app-client-id>"
dotnet user-secrets set "SwaggerUI:ClientId" "<client-app-client-id>"
```

**Option B: Environment Variables (Production)**

```bash
export AzureAd__Instance="https://login.microsoftonline.com/"
export AzureAd__TenantId="<your-tenant-id>"
export AzureAd__ClientId="<server-app-client-id>"
export AzureAd__ClientSecret="<server-app-client-secret>"
export AzureAd__Audience="api://<server-app-client-id>"
export SwaggerUI__ClientId="<client-app-client-id>"
```

**Option C: appsettings.json**

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<server-app-client-id>",
    "ClientSecret": "<server-app-client-secret>",
    "Audience": "api://<server-app-client-id>"
  },
  "SwaggerUI": {
    "ClientId": "<client-app-client-id>"
  }
}
```

### 3.2 Start the server

```powershell
cd ApiGraphActivator
dotnet run
```

---

## Step 4: Test Authentication

### 4.1 Test with Swagger UI

1. Navigate to `https://localhost:7189/swagger`
2. Click the **Authorize** button (lock icon)
3. Select the scopes you need (`Mcp.Read` and/or `Mcp.ReadWrite`)
4. Click **Authorize** — you'll be redirected to Microsoft login
5. Sign in with your Entra ID account
6. After authorization, test any endpoint — the Bearer token is automatically included

### 4.2 Test with curl (Client Credentials flow)

For service-to-service scenarios, you can obtain a token using client credentials. Note: for client credentials, you need to add an **Application permission** to the server app instead of delegated scopes.

```powershell
# Get a token using the client credentials flow
$tokenResponse = Invoke-RestMethod -Method Post `
    -Uri "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token" `
    -Body @{
        client_id     = "<client-app-client-id>"
        client_secret = "<client-app-secret>"
        scope         = "api://<server-app-client-id>/.default"
        grant_type    = "client_credentials"
    }

$token = $tokenResponse.access_token

# Test the MCP endpoint
Invoke-RestMethod -Method Post `
    -Uri "https://localhost:7189/mcp" `
    -Headers @{ Authorization = "Bearer $token" } `
    -ContentType "application/json" `
    -Body '{
        "jsonrpc": "2.0",
        "id": "1",
        "method": "initialize",
        "params": {}
    }'
```

### 4.3 Test with Azure CLI (delegated flow)

```powershell
# Login to Azure CLI
az login

# Get a token for your API
$token = az account get-access-token `
    --resource "api://<server-app-client-id>" `
    --query accessToken -o tsv

# Test the MCP tools/list endpoint
Invoke-RestMethod -Method Post `
    -Uri "https://localhost:7189/mcp" `
    -Headers @{ Authorization = "Bearer $token" } `
    -ContentType "application/json" `
    -Body '{
        "jsonrpc": "2.0",
        "id": "2",
        "method": "tools/list",
        "params": {}
    }'
```

---

## Step 5: Configure MCP Clients

### 5.1 Claude Desktop / Copilot CLI (stdio mode)

The stdio transport mode (`--mcp-stdio`) bypasses HTTP authentication since it communicates locally via stdin/stdout. No OAuth configuration is needed for local MCP clients.

### 5.2 HTTP-based MCP Clients

Any HTTP-based MCP client must include a Bearer token in the `Authorization` header:

```
POST /mcp HTTP/1.1
Host: your-server.example.com
Authorization: Bearer <access-token>
Content-Type: application/json

{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/list",
    "params": {}
}
```

---

## Authorization Scopes Reference

| Scope | Access Level | Tools Available |
|-------|-------------|-----------------|
| `Mcp.Read` | Read-only | `search_documents`, `get_document_content`, `analyze_document`, `get_crawl_status`, `get_last_crawl_info`, `get_crawled_companies`, `list_companies` |
| `Mcp.ReadWrite` | Full access | All read tools + `start_crawl`, `manage_connections` |

The `tools/list` response is automatically filtered based on the caller's scope — clients with only `Mcp.Read` will not see write tools.

---

## Security Best Practices

1. **Rotate client secrets** before they expire. Set calendar reminders.
2. **Use certificates** instead of client secrets for production server-to-server authentication.
3. **Restrict token lifetime** via Conditional Access policies.
4. **Monitor sign-in logs** in Entra ID for unauthorized access attempts.
5. **Use Managed Identity** when deploying to Azure (eliminates client secrets entirely).
6. **Enable Conditional Access** to require MFA, compliant devices, or specific locations.

---

## Troubleshooting

### Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `401 Unauthorized` | No token or invalid token | Ensure `Authorization: Bearer <token>` header is present with a valid token |
| `401 IDX10214: Audience validation failed` | Token audience doesn't match | Verify `AzureAd:Audience` matches the Application ID URI (e.g., `api://<client-id>`) |
| `401 IDX10205: Issuer validation failed` | Token issuer doesn't match tenant | Verify `AzureAd:TenantId` is correct |
| `403 Forbidden` / `Insufficient permissions` | Missing required scope | Ensure the client app has the correct API permissions and admin consent is granted |
| `AADSTS65001: The user or administrator has not consented` | Missing consent | Grant admin consent in the client app's API permissions |
| `AADSTS700016: Application not found` | Wrong client ID | Double-check `AzureAd:ClientId` and `SwaggerUI:ClientId` |

### Verify Token Contents

Decode your JWT token at [jwt.ms](https://jwt.ms) and verify:
- `aud` (audience) matches your Application ID URI
- `iss` (issuer) matches `https://login.microsoftonline.com/<tenant-id>/v2.0`
- `scp` (scope) contains `Mcp.Read` or `Mcp.ReadWrite`
- `exp` (expiration) hasn't passed

---

## Summary of Required App Registrations

| Registration | Purpose | Key Configuration |
|-------------|---------|-------------------|
| **SEC-Edgar-MCP-Server** | API/Server identity | Exposes `Mcp.Read` and `Mcp.ReadWrite` scopes; holds client secret; has Graph application permissions |
| **SEC-Edgar-MCP-Client** | Client apps (Swagger, React, MCP tools) | SPA redirect URIs; delegated permissions to the server API; no client secret needed for SPA |
