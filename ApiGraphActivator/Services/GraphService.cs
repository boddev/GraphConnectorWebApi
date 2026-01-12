using Azure.Identity;
using Microsoft.Graph;

static class GraphService
{
  static GraphServiceClient? _client;

  public static GraphServiceClient Client
  {
    get
    {
      if (_client is null)
      {
        var tenantId = GetSetting("AzureAd:TenantId") ?? GetSetting("AZURE_TENANT_ID");
        var clientId = GetSetting("AzureAd:ClientId") ?? GetSetting("AZURE_CLIENT_ID");
        var clientSecret = GetSetting("AzureAd:ClientSecret") ?? GetSetting("AZURE_CLIENT_SECRET");

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
          throw new InvalidOperationException(
            "Missing Azure AD client credentials for Microsoft Graph. " +
            "Set environment variables AzureAd:TenantId, AzureAd:ClientId, AzureAd:ClientSecret (or AZURE_TENANT_ID/AZURE_CLIENT_ID/AZURE_CLIENT_SECRET). " +
            "If you are running from Visual Studio/VS Code, ensure the active launch profile defines these variables.");
        }

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _client = new GraphServiceClient(credential);
      }

      return _client;
    }
  }

  private static string? GetSetting(string key)
  {
    // Note: this project currently uses environment variables with ':' in the name (e.g. AzureAd:ClientId).
    // Some runtimes/deployments may prefer the standard AZURE_* environment variable names.
    return Environment.GetEnvironmentVariable(key);
  }
}