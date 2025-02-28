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
        // Build the configuration from user secrets
        var builder = new ConfigurationBuilder().AddUserSecrets<GraphService>();
        var config = builder.Build();

        // Retrieve the Azure AD credentials from the configuration
        var clientId = config["AzureAd:ClientId"];
        var clientSecret = config["AzureAd:ClientSecret"];
        var tenantId = config["AzureAd:TenantId"];

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