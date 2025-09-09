using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.Services
{
    // Custom JSON converter for ID field that can handle string, number, or null
    public class MCPIdConverter : JsonConverter<object?>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetInt32(),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Unexpected token type for ID: {reader.TokenType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else if (value is string str)
            {
                writer.WriteStringValue(str);
            }
            else if (value is int num)
            {
                writer.WriteNumberValue(num);
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }

    // MCP Protocol Models
    public class MCPRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        
        [JsonPropertyName("id")]
        [JsonConverter(typeof(MCPIdConverter))]
        public object? Id { get; set; }
        
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
        [JsonConverter(typeof(MCPIdConverter))]
        public object? Id { get; set; }
        
        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }
        
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MCPError? Error { get; set; }
        
        // Ensure only result OR error is set, never both
        public void SetResult(object? result)
        {
            Result = result;
            Error = null;
        }
        
        public void SetError(MCPError error)
        {
            Error = error;
            Result = null;
        }
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
