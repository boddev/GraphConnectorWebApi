using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;

class GraphService
{
  static GraphServiceClient? _client;

  public static GraphServiceClient Client
  {
    get
    {
      if (_client is null)
      {
        var builder = new ConfigurationBuilder().AddUserSecrets<GraphService>();
        var config = builder.Build();

        var clientId = config["AzureAd:ClientId"];
        var clientSecret = config["AzureAd:ClientSecret"];
        var tenantId = config["AzureAd:TenantId"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _client = new GraphServiceClient(credential);
      }

      return _client;
    }
  }
}