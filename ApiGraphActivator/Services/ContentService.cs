using ApiGraphActivator;
using ApiGraphActivator.Services;
using Markdig;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.Extensions.Logging;

// Define a static class named ContentService
static class ContentService
{
  // Define a static list to hold EdgarExternalItem objects
  static List<EdgarExternalItem> content;

  static ILogger logger = ConnectionService.logger;

  // Define a static list to hold ExternalItem objects
  static List<ExternalItem> items = new();

  // Helper method to get default connection ID
  private static async Task<string> GetDefaultConnectionIdAsync()
  {
    try
    {
      // Create a logger factory and service instance to get the default connection
      var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
      var serviceLogger = loggerFactory.CreateLogger<ExternalConnectionManagerService>();
      var connectionService = new ExternalConnectionManagerService(serviceLogger);
      var defaultConnectionId = await connectionService.GetDefaultConnectionIdAsync();
      
      // Fall back to hardcoded connection if no default is found
      return defaultConnectionId ?? ConnectionConfiguration.ExternalConnection.Id!;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error getting default connection ID, using hardcoded fallback");
      return ConnectionConfiguration.ExternalConnection.Id!;
    }
  }

  // Define an asynchronous static method named Extract
  async static Task Extract()
  {
    // Populate the content list with data from the EdgarService
    content = await EdgarService.HydrateLookupData();
  }

  // Define a static method named Transform that takes an EdgarExternalItem as a parameter
  // DEPRECATED: This method is now simplified since ExternalItem creation moved to EdgarService
  public static void Transform(EdgarExternalItem item, string? connectionId = null)
  {
    // This method is kept for backward compatibility but the main logic has moved to EdgarService
    // The EdgarService now creates the full ExternalItem and calls ContentService.Load directly
    logger?.LogWarning("Transform method called - this is deprecated. ExternalItem creation should be handled in EdgarService.");
  }

  // Content enhancement method for better Copilot understanding
  public static string EnhanceContentForCopilot(string content, string company, string form)
  {
    try
    {
      // Add semantic markup for better Copilot understanding
      var enhancedContent = $@"
      <article data-company='{System.Web.HttpUtility.HtmlEncode(company)}' data-form-type='{form}' data-document-type='sec-filing'>
        <header>
          <h1>Company: {System.Web.HttpUtility.HtmlEncode(company)} | Filing Type: {form}</h1>
          <meta name='company' content='{System.Web.HttpUtility.HtmlEncode(company)}' />
          <meta name='form-type' content='{form}' />
          <meta name='source' content='SEC EDGAR Database' />
        </header>
        <main data-content-type='financial-disclosure'>
          {content}
        </main>
      </article>";
      
      return enhancedContent;
    }
    catch (Exception ex)
    {
      logger?.LogWarning($"Error enhancing content for Copilot: {ex.Message}");
      return content; // Return original content if enhancement fails
    }
  }

  // Define an asynchronous static method named Load
  public static async Task Load(ExternalItem item, string? connectionId = null)
  {
    // Require connectionId - do not process if null
    if (string.IsNullOrEmpty(connectionId))
    {
      logger.LogWarning("Load called with null/empty connectionId. Skipping content loading to prevent unwanted processing.");
      return;
    }
    
    // Log which connection ID is being used
    logger.LogInformation("Loading content to connection: {ConnectionId}", connectionId);
    
    // Iterate over each item in the items list
    //foreach (var item in items)
    {
      // Output a message to the console indicating the start of the item loading process
      logger.LogTrace(string.Format("Loading item {0} to connection {1}...", item.Id, connectionId));
      
      // Validate the item has required properties
      if (item.Properties?.AdditionalData == null)
      {
        logger.LogError("Item {ItemId} has no properties - skipping", item.Id);
        return;
      }
      
      // Log some key properties for debugging
      logger.LogDebug("Item properties: Title='{Title}', Company='{Company}', Url='{Url}'", 
        item.Properties.AdditionalData.ContainsKey("Title") ? item.Properties.AdditionalData["Title"] : "", 
        item.Properties.AdditionalData.ContainsKey("Company") ? item.Properties.AdditionalData["Company"] : "", 
        item.Properties.AdditionalData.ContainsKey("Url") ? item.Properties.AdditionalData["Url"] : "");
      
      // Log all properties for debugging
      logger.LogDebug("All item properties: {@Properties}", 
        item.Properties.AdditionalData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "null"));
      
      // Validate required properties
      var requiredProperties = new[] { "Title", "Company", "Url", "IconUrl" };
      var missingProperties = requiredProperties.Where(prop => 
        !item.Properties.AdditionalData.ContainsKey(prop) || 
        item.Properties.AdditionalData[prop] == null || 
        string.IsNullOrEmpty(item.Properties.AdditionalData[prop].ToString())).ToList();
        
      if (missingProperties.Any())
      {
        logger.LogWarning("Item {ItemId} has missing required properties: {MissingProperties}", 
          item.Id, string.Join(", ", missingProperties));
      }
        
      try
      {
      // Log the item being sent for debugging
      logger.LogInformation("Attempting to PUT item {ItemId} with properties count: {PropertyCount}", 
        item.Id, item.Properties?.AdditionalData?.Count ?? 0);
      
      // Log content details
      logger.LogDebug("Item content - Type: {ContentType}, Length: {ContentLength}", 
        item.Content?.Type, item.Content?.Value?.Length ?? 0);
      
      // Log ACL details  
      logger.LogDebug("Item ACL count: {AclCount}", item.Acl?.Count ?? 0);        // Await the asynchronous operation of putting the item into the GraphService client
        // await GraphService.Client.External
        //   .Connections[Uri.EscapeDataString(connectionId)]
        //   .Items[item.Id]
        //   .PutAsync(item);
        // // Output a message to the console indicating the completion of the item loading process
        logger.LogTrace($"{item.Id} completed.");

        // Get the URL from the item's AdditionalData dictionary
        if (item.Properties?.AdditionalData?.ContainsKey("Url") == true)
        {
          string url = (string)item.Properties.AdditionalData["Url"];
          // Await the asynchronous operation of updating the processed item in the EdgarService
          await EdgarService.UpdateProcessedItem(url);
          logger.LogTrace($"ProcessedForms table updated for {url}.");
        }
      }
      catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
      {
        logger.LogError("OData error loading item {ItemId} to connection {ConnectionId}", item.Id, connectionId);
        logger.LogError("Error code: {ErrorCode}, Error message: {ErrorMessage}", 
          odataEx.Error?.Code, odataEx.Error?.Message);
        if (odataEx.Error?.InnerError != null)
        {
          logger.LogError("Inner error: {@InnerError}", odataEx.Error.InnerError.AdditionalData);
        }
        logger.LogError("Full OData exception: {Exception}", odataEx.ToString());
      }
      catch (HttpRequestException httpEx)
      {
        logger.LogError("HTTP error loading item {ItemId} to connection {ConnectionId}", item.Id, connectionId);
        logger.LogError("HTTP error: {HttpError}", httpEx.Message);
        logger.LogError("Full HTTP exception: {Exception}", httpEx.ToString());
      }
      catch (Exception ex)
      {
        // Output an error message to the console if an exception occurs
        logger.LogError("Error loading item {ItemId} to connection {ConnectionId}", item.Id, connectionId);
        logger.LogError("Item details - Title: {Title}, Company: {Company}, Url: {Url}", 
          item.Properties?.AdditionalData?["Title"], 
          item.Properties?.AdditionalData?["Company"], 
          item.Properties?.AdditionalData?["Url"]);
        logger.LogError("Full error details: {ErrorMessage}", ex.Message);
        if (ex.InnerException != null)
        {
          logger.LogError("Inner exception: {InnerException}", ex.InnerException.Message);
        }
        logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
      }
    }
  }

  // Define a public asynchronous static method named LoadContent
  public static async Task LoadContent(string? connectionId = null)
  {
    // Require connectionId - do not process if null
    if (string.IsNullOrEmpty(connectionId))
    {
      logger.LogWarning("LoadContent called with null/empty connectionId. Skipping content loading to prevent unwanted processing.");
      return;
    }
    
    logger.LogInformation("Loading content using connection: {ConnectionId}", connectionId);
    
    // Call the Extract method to populate the content list
    await Extract();

    // // Iterate over each item in the content list and transform it
    // foreach (var item in content)
    // {
    //   Transform(item);
    // }

    // // Call the Load method to load the transformed items
    // await Load();
  }

  // Define a public asynchronous static method named LoadContentForCompanies
  public static async Task LoadContentForCompanies(List<Company> companies, string? connectionId = null)
  {
    // Require connectionId - do not process if null
    if (string.IsNullOrEmpty(connectionId))
    {
      logger.LogWarning("LoadContentForCompanies called with null/empty connectionId. Skipping content loading to prevent unwanted processing.");
      return;
    }
    
    logger.LogInformation("Starting content extraction for {CompanyCount} companies using connection: {ConnectionId}", companies.Count, connectionId);
    
    // Extract content for specific companies
    await ExtractForCompanies(companies, connectionId);

    // // Iterate over each item in the content list and transform it
    // foreach (var item in content)
    // {
    //   Transform(item);
    // }

    // // Call the Load method to load the transformed items
    // await Load();
    
    logger.LogInformation("Completed content extraction for {CompanyCount} companies using connection: {ConnectionId}", companies.Count, connectionId);
  }

  // Define an asynchronous static method named ExtractForCompanies
  async static Task ExtractForCompanies(List<Company> companies, string? connectionId = null)
  {
    logger.LogInformation("Extracting data for selected companies: {Companies} using connection: {ConnectionId}", 
      string.Join(", ", companies.Select(c => c.Ticker)), connectionId ?? "null");
    
    // Pass the selected companies to EdgarService for processing
    content = await EdgarService.HydrateLookupDataForCompanies(companies, connectionId);
    
    logger.LogInformation("Extracted {ItemCount} items for selected companies using connection: {ConnectionId}", content?.Count ?? 0, connectionId ?? "null");
  }
}