using ApiGraphActivator;
using ApiGraphActivator.Services;
using Markdig;
using Microsoft.Graph.Models.ExternalConnectors;


static class ContentService
{
  static List<EdgarExternalItem> content;
  
  static List<ExternalItem> items = new();
  async static Task Extract()
  {
     content = await EdgarService.HydrateLookupData();

  }

  static void Transform(EdgarExternalItem item)
  {
    ExternalItem exItem =  new ExternalItem
      {
        Id = EdgarService.itemId,
        Properties = new()
        {
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
          Value = item.contentField,
          Type = ExternalItemContentType.Html
        },
        Acl = new()
        {
          new()
          {
            Type = AclType.Group,
            Value = "EdgarDataUsers",
            AccessType = AccessType.Grant
          }

        }
      };

    items.Add(exItem);

  }

  static async Task Load()
  {
    foreach (var item in items)
    {
      Console.Write(string.Format("Loading item {0}...", item.Id));
      try
      {
        await GraphService.Client.External
          .Connections[Uri.EscapeDataString(ConnectionConfiguration.ExternalConnection.Id!)]
          .Items[item.Id]
          .PutAsync(item);
        Console.WriteLine("DONE");

        string url = (string)item.AdditionalData["Url"];
        await EdgarService.UpdateProcessedItem(url);
      }
      catch (Exception ex)
      {
        Console.WriteLine("ERROR");
        Console.WriteLine(ex.Message);
      }
    }
  }

  public static async Task LoadContent()
  {
    await Extract();

    foreach (var item in content)
    {
        Transform(item);
    }
        
    await Load();
  }
}