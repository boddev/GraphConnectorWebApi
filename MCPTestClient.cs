using System.Text;
using System.Text.Json;

namespace ApiGraphActivator.Tests
{
    public class MCPTestClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public MCPTestClient(string baseUrl = "https://localhost:7000")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
        }

        public async Task<string> TestInitialize()
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = "1",
                method = "initialize",
                @params = new { }
            };

            return await SendRequest(request);
        }

        public async Task<string> TestToolsList()
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = "2",
                method = "tools/list",
                @params = new { }
            };

            return await SendRequest(request);
        }

        public async Task<string> TestSearchDocuments(string query, string? company = null)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = "3",
                method = "tools/call",
                @params = new
                {
                    name = "search_documents",
                    arguments = new
                    {
                        query = query,
                        company = company
                    }
                }
            };

            return await SendRequest(request);
        }

        public async Task<string> TestListCompanies()
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = "4",
                method = "tools/call",
                @params = new
                {
                    name = "list_companies",
                    arguments = new { }
                }
            };

            return await SendRequest(request);
        }

        private async Task<string> SendRequest(object request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                Console.WriteLine($"Sending request to {_baseUrl}/mcp:");
                Console.WriteLine(json);
                Console.WriteLine();

                var response = await _httpClient.PostAsync($"{_baseUrl}/mcp", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Response ({response.StatusCode}):");
                Console.WriteLine(responseBody);
                Console.WriteLine(new string('-', 50));

                return responseBody;
            }
            catch (Exception ex)
            {
                var error = $"Error: {ex.Message}";
                Console.WriteLine(error);
                return error;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Simple console program to test the MCP server
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new MCPTestClient();

            Console.WriteLine("Testing MCP Server...");
            Console.WriteLine();

            // Test initialization
            await client.TestInitialize();

            // Test tools list
            await client.TestToolsList();

            // Test search documents
            await client.TestSearchDocuments("financial statements", "Apple");

            // Test list companies
            await client.TestListCompanies();

            client.Dispose();
            Console.WriteLine("Tests completed. Press any key to exit...");
            Console.ReadKey();
        }
    }
}
