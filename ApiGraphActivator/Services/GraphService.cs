using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;

class GraphService
{
  // Define a static field to hold the GraphServiceClient instance
  static GraphServiceClient? _client;

  // Define a public static property to get the GraphServiceClient instance
  public static GraphServiceClient Client
  {
    get
    {
      // If the _client field is null, initialize it
      if (_client is null)
      {

        // Retrieve the Azure AD credentials from the configuration
        var clientId = Environment.GetEnvironmentVariable("AzureAd:ClientId");
        var clientSecret = Environment.GetEnvironmentVariable("AzureAd:ClientSecret");
        var tenantId = Environment.GetEnvironmentVariable("AzureAd:TenantId");

        // Create a ClientSecretCredential using the retrieved credentials
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        // Initialize the GraphServiceClient with the credential
        _client = new GraphServiceClient(credential);
      }

      // Return the initialized GraphServiceClient instance
      return _client;
    }
  }
}