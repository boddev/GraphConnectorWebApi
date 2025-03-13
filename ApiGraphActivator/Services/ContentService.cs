using ApiGraphActivator;
using ApiGraphActivator.Services;
using Markdig;
using Microsoft.Graph.Models.ExternalConnectors;

// Define a static class named ContentService
static class ContentService
{
  // Define a static list to hold EdgarExternalItem objects
  static List<EdgarExternalItem> content;

  static ILogger logger = ConnectionService.logger;

  // Define a static list to hold ExternalItem objects
  static List<ExternalItem> items = new();

  // Define an asynchronous static method named Extract
  async static Task Extract()
  {
    // Populate the content list with data from the EdgarService
    content = await EdgarService.HydrateLookupData();
  }

  // Define a static method named Transform that takes an EdgarExternalItem as a parameter
  public static async void Transform(EdgarExternalItem item)
  {
    // Create a new ExternalItem object and populate its properties
    ExternalItem exItem = new ExternalItem
    {
      Id = EdgarService.itemId, // Set the Id property using the EdgarService itemId
      
      Properties = new()
      {
        // Set the AdditionalData dictionary with various properties from the EdgarExternalItem
        // This applies to the schema that was defined in the ConnectionService
        AdditionalData = new Dictionary<string, object> {
            { "Title", item.titleField },
            { "Company", item.companyField },
            { "Url", item.urlField },
            { "DateFiled", item.reportDateField },
            { "Form", item.formField }
          }
      },
      Content = new()
      {
        // Set the content and type of the ExternalItem
        Value = item.contentField,
        Type = ExternalItemContentType.Html
      },
      Acl = new()
        {
          // Set the Access Control List (ACL) for the ExternalItem
          new()
          {
            Type = AclType.Everyone,
            Value = "Everyone",
            AccessType = AccessType.Grant
          }
        }
    };

    await Load(exItem);
    // Add the created ExternalItem to the items list
    items.Add(exItem);
  }

  // Define an asynchronous static method named Load
  static async Task Load(ExternalItem item)
  {
    // Iterate over each item in the items list
    //foreach (var item in items)
    {
      // Output a message to the console indicating the start of the item loading process
      logger.LogInformation(string.Format("Loading item {0}...", item.Id));
      try
      {
        // Await the asynchronous operation of putting the item into the GraphService client
        await GraphService.Client.External
          .Connections[Uri.EscapeDataString(ConnectionConfiguration.ExternalConnection.Id!)]
          .Items[item.Id]
          .PutAsync(item);
        // Output a message to the console indicating the completion of the item loading process
        logger.LogInformation($"{item.Id} completed.");

        // Get the URL from the item's AdditionalData dictionary
        string url = (string)item.Properties.AdditionalData["Url"];
        // Await the asynchronous operation of updating the processed item in the EdgarService
        await EdgarService.UpdateProcessedItem(url);
        logger.LogInformation($"ProcessedForms table updated for {url}.");
      }
      catch (Exception ex)
      {
        // Output an error message to the console if an exception occurs
        logger.LogError("ERROR");
        logger.LogError(ex.Message);
      }
    }
  }

  // Define a public asynchronous static method named LoadContent
  public static async Task LoadContent()
  {
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
}