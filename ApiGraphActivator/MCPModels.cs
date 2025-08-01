using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.Services
{
    // MCP Protocol Models
    public class MCPRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("method")]
        public string Method { get; set; } = "";
        
        [JsonPropertyName("params")]
        public JsonElement Params { get; set; }
    }

    public class MCPResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("result")]
        public object? Result { get; set; }
        
        [JsonPropertyName("error")]
        public MCPError? Error { get; set; }
    }

    public class MCPError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class ToolCallRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("arguments")]
        public JsonElement Arguments { get; set; }
    }
}
