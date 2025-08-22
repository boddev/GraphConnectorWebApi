using Microsoft.Graph.Models.ExternalConnectors;
using System.Text.Json;
using ApiGraphActivator;

public class ExternalConnectionManagerService
{
    private readonly ILogger<ExternalConnectionManagerService> _logger;
    private readonly string _connectionsFilePath = Path.Combine("data", "external-connections.json");

    public ExternalConnectionManagerService(ILogger<ExternalConnectionManagerService> logger)
    {
        _logger = logger;
        EnsureDataDirectory();
    }

    private void EnsureDataDirectory()
    {
        var dataDir = Path.GetDirectoryName(_connectionsFilePath);
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir!);
        }
    }

    public async Task<List<ExternalConnectionInfo>> GetConnectionsAsync()
    {
        try
        {
            if (!File.Exists(_connectionsFilePath))
            {
                // Return default connection if no custom connections exist
                return new List<ExternalConnectionInfo>
                {
                    new ExternalConnectionInfo
                    {
                        Id = ConnectionConfiguration.ExternalConnection.Id!,
                        Name = ConnectionConfiguration.ExternalConnection.Name!,
                        Description = ConnectionConfiguration.ExternalConnection.Description!,
                        IsDefault = true,
                        CreatedDate = DateTime.UtcNow
                    }
                };
            }

            var json = await File.ReadAllTextAsync(_connectionsFilePath);
            var connections = JsonSerializer.Deserialize<List<ExternalConnectionInfo>>(json) ?? new List<ExternalConnectionInfo>();
            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading external connections");
            return new List<ExternalConnectionInfo>();
        }
    }

    public async Task<(bool Success, ExternalConnectionInfo? Result, string ErrorMessage)> CreateConnectionAsync(CreateExternalConnectionRequest request)
    {
        try
        {
            // Validate connection ID format
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return (false, null, "Connection ID is required");
            }

            // Microsoft Graph connection ID requirements:
            // - 3-32 characters
            // - Lowercase letters, numbers, and underscores only (no uppercase)
            // - Cannot start with 'Microsoft'
            // - Must start with a letter
            if (request.Id.Length < 3 || request.Id.Length > 32)
            {
                return (false, null, "Connection ID must be between 3 and 32 characters");
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Id, @"^[a-z][a-z0-9_]*$"))
            {
                return (false, null, "Connection ID must start with a lowercase letter and contain only lowercase letters, numbers, and underscores");
            }

            if (request.Id.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "Connection ID cannot start with 'microsoft'");
            }

            // Validate name and description
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (false, null, "Connection Name is required");
            }

            if (request.Name.Length > 128)
            {
                return (false, null, "Connection Name must be 128 characters or less");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return (false, null, "Connection Description is required");
            }

            if (request.Description.Length > 1024)
            {
                return (false, null, "Connection Description must be 1024 characters or less");
            }

            // Sanitize description - remove line breaks and excessive whitespace
            request.Description = System.Text.RegularExpressions.Regex.Replace(
                request.Description.Trim(), 
                @"\s+", " "); // Replace multiple whitespace/newlines with single space

            if (request.Description.Length > 500)  // Further restrict for Graph API
            {
                request.Description = request.Description.Substring(0, 500).Trim();
            }

            // Check if connection already exists
            var existingConnections = await GetConnectionsAsync();
            if (existingConnections.Any(c => c.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, null, $"Connection with ID '{request.Id}' already exists");
            }

            _logger.LogInformation($"Creating connection with ID: '{request.Id}', Name: '{request.Name}', Description: '{request.Description}'");

            var connectionInfo = new ExternalConnectionInfo
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                IsDefault = false,
                CreatedDate = DateTime.UtcNow
            };

            // Create the connection in Microsoft Graph
            var connection = new ExternalConnection
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description
            };

            var result = await GraphService.Client.External.Connections.PostAsync(connection);
            _logger.LogInformation($"Connection created successfully with ID: {result?.Id}");

            // Create schema for the connection
            await CreateSchemaForConnectionAsync(request.Id);

            // Save to local file
            await SaveConnectionInfoAsync(connectionInfo);

            return (true, connectionInfo, string.Empty);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataError)
        {
            string errorDetails = $"Error code: {odataError.Error?.Code}, Message: {odataError.Error?.Message}";
            _logger.LogError($"Microsoft Graph API error creating connection '{request.Id}': {errorDetails}");
            
            // Additional details for debugging
            if (odataError.Error?.Details != null)
            {
                foreach (var detail in odataError.Error.Details)
                {
                    _logger.LogError($"Additional error detail - Code: {detail.Code}, Message: {detail.Message}");
                }
            }
            
            // Provide more user-friendly error messages
            string userFriendlyMessage = odataError.Error?.Code switch
            {
                "InvalidRequest" => "The connection request is invalid. Please check the connection ID format (3-32 characters, letters/numbers/underscores only).",
                "Conflict" => "A connection with this ID already exists. Please choose a different ID.",
                "Forbidden" => "You don't have permission to create external connections. Please check your Microsoft Graph permissions.",
                _ => errorDetails
            };
            
            return (false, null, userFriendlyMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating connection");
            return (false, null, ex.Message);
        }
    }

    private async Task CreateSchemaForConnectionAsync(string connectionId)
    {
        _logger.LogInformation($"Creating schema for connection: {connectionId}");

        Schema schema = ConnectionConfiguration.Schema;
        await GraphService.Client.External
            .Connections[connectionId]
            .Schema
            .PatchAsync(schema);

        _logger.LogInformation($"Schema created successfully for connection: {connectionId}");
    }

    private async Task SaveConnectionInfoAsync(ExternalConnectionInfo connectionInfo)
    {
        var connections = await GetConnectionsAsync();
        connections.Add(connectionInfo);

        var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_connectionsFilePath, json);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteConnectionAsync(string connectionId)
    {
        try
        {
            // Don't allow deletion of default connection
            if (connectionId == ConnectionConfiguration.ExternalConnection.Id)
            {
                return (false, "Cannot delete the default connection");
            }

            // Delete from Microsoft Graph
            await GraphService.Client.External.Connections[connectionId].DeleteAsync();
            _logger.LogInformation($"Connection deleted successfully: {connectionId}");

            // Remove from local file
            var connections = await GetConnectionsAsync();
            connections.RemoveAll(c => c.Id == connectionId);

            var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_connectionsFilePath, json);

            return (true, string.Empty);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataError)
        {
            string errorDetails = $"Error code: {odataError.Error?.Code}, Message: {odataError.Error?.Message}";
            _logger.LogError($"Failed to delete connection: {errorDetails}");
            return (false, errorDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting connection");
            return (false, ex.Message);
        }
    }

    public async Task<string?> GetDefaultConnectionIdAsync()
    {
        try
        {
            var connections = await GetConnectionsAsync();
            var defaultConnection = connections.FirstOrDefault(c => c.IsDefault);
            return defaultConnection?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default connection");
            return null;
        }
    }
}

public class ExternalConnectionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedDate { get; set; }
}

public class CreateExternalConnectionRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class LoadContentToConnectionRequest
{
    public List<Company> Companies { get; set; } = new List<Company>();
    public string ConnectionId { get; set; } = string.Empty;
}
