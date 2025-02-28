using System.Text.Json;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ExternalConnectors;

static class ConnectionConfiguration
{
  // Define a private static field to hold the layout dictionary
  private static Dictionary<string, object>? _layout;

  // Define a private static property to get the layout dictionary
  private static Dictionary<string, object> Layout
  {
    get
    {
      // If the layout dictionary is null, read and deserialize the JSON file
      if (_layout is null)
      {
        var adaptiveCard = File.ReadAllText("resultLayout.json");
        _layout = JsonSerializer.Deserialize<Dictionary<string, object>>(adaptiveCard);
      }

      // Return the layout dictionary
      return _layout!;
    }
  }

  // Define a public static property to get the ExternalConnection object
  public static ExternalConnection ExternalConnection
  {
    get
    {
      // Return a new ExternalConnection object with predefined properties
      return new ExternalConnection
      {
        Id = "secedgardata",
        Name = "SecEdgarData",
        Description = "Filings from the SEC EDGAR database",
        //SearchSettings = new()
        //{
        //  SearchResultTemplates = new() {
        //    new() {
        //      Id = "secedgardata",
        //      Priority = 1,
        //      Layout = new Json {
        //        AdditionalData = Layout
        //      }
        //    }
        //  }
        //}
      };
    }
  }

  public static Schema Schema
  {
    get
    {
      // Return a new Schema object with predefined properties
      // Labels Title, Url and IconUrl are required for Copilot usage
      return new Schema
      {
        BaseType = "microsoft.graph.externalItem",
        Properties = new()
        {
          new Property
          {
            Name = "Title",
            Type = PropertyType.String,
            IsQueryable = true,
            IsSearchable = true,
            IsRetrievable = true,
            Labels = new() { Label.Title }
          },
          new Property
          {
            Name = "Company",
            Type = PropertyType.String,
            IsRetrievable = true,
            IsSearchable = true,
            IsQueryable = true
          },
          new Property
          {
            Name = "Url",
            Type = PropertyType.String,
            IsRetrievable = true,
            Labels = new() { Label.Url }
          },
          new Property
          {
            Name = "IconUrl",
            Type = PropertyType.String,
            IsRetrievable = true,
            Labels = new() { Label.IconUrl }
          },
          new Property
          {
            Name = "Form",
            Type = PropertyType.String,
            IsRetrievable = true,
            IsSearchable = true,
            IsQueryable = true
          },
          new Property
          {
            Name = "DateFiled",
            Type = PropertyType.DateTime,
            IsRetrievable = true,
            IsSearchable = true,
            IsQueryable = true,
            Labels = new() { Label.CreatedDateTime }
          }
        }
      };
    }
  }
}