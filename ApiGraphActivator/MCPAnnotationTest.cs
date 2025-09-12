using System.Text.Json;
using ApiGraphActivator.Services;

namespace ApiGraphActivator.Testing
{
    public class MCPAnnotationTest
    {
        public static void ShowToolsListWithAnnotations()
        {
            // Create a sample tools list response to show the annotations format
            var tools = new object[]
            {
                new
                {
                    name = "search_documents",
                    description = "Search SEC documents by company, form type, or content",
                    annotations = new
                    {
                        title = "Search SEC Documents",
                        readOnlyHint = true
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query" },
                            company = new { type = "string", description = "Company name or ticker" },
                            formType = new { type = "string", description = "SEC form type (10-K, 10-Q, etc.)" },
                            dateRange = new { type = "string", description = "Date range filter" }
                        },
                        required = new[] { "query" }
                    }
                }
            };

            var response = new
            {
                jsonrpc = "2.0",
                id = 1,
                result = new { tools }
            };

            Console.WriteLine("Sample tools/list response with annotations:");
            Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
