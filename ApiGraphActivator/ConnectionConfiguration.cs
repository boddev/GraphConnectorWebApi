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
            Labels = new() { Label.CreatedDateTime }
          },
          new Property
          {
            Name = "ContentSummary",
            Type = PropertyType.String,
            IsSearchable = true,
            IsQueryable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "KeyFinancialMetrics",
            Type = PropertyType.String,
            IsSearchable = true,
            IsQueryable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "BusinessSegments",
            Type = PropertyType.String,
            IsSearchable = true,
            IsQueryable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "IndustryCategory",
            Type = PropertyType.String,
            IsSearchable = true,
            IsQueryable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "CompetitiveAdvantages",
            Type = PropertyType.String,
            IsSearchable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "ESGContent",
            Type = PropertyType.String,
            IsSearchable = true,
            IsRetrievable = true
          },
          new Property
          {
            Name = "UUID",
            Type = PropertyType.String,
            IsQueryable = true,
            IsSearchable = false,
            IsRetrievable = true
          }
        }
      };
    }
  }
}