using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class AzureStorageService : ICrawlStorageService
{
    private readonly ILogger<AzureStorageService> _logger;
    private readonly StorageConfiguration _config;
    private TableClient? _tableClient;
    private BlobServiceClient? _blobServiceClient;

    public AzureStorageService(ILogger<AzureStorageService> logger, StorageConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_config.AzureConnectionString))
            {
                throw new InvalidOperationException("Azure connection string is required");
            }

            _tableClient = new TableClient(_config.AzureConnectionString, _config.ProcessedTableName);
            _blobServiceClient = new BlobServiceClient(_config.AzureConnectionString);

            if (_config.AutoCreateTables)
            {
                await _tableClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Ensured table exists: {TableName}", _config.ProcessedTableName);

                var containerClient = _blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);
                await containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation("Ensured blob container exists: {ContainerName}", _config.BlobContainerName);
            }

            _logger.LogInformation("Azure storage initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure storage");
            throw;
        }
    }

    public async Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url)
    {
        if (_tableClient == null)
        {
            _logger.LogWarning("Azure Table Storage not initialized. Skipping document tracking.");
            return;
        }

        try
        {
            // Check if document already exists
            string filter = $"Url eq '{url}'";
            var existingResults = _tableClient.Query<TableEntity>(filter).ToList();
            
            if (existingResults.Count > 0)
            {
                _logger.LogTrace("Document already tracked: {Url}", url);
                return;
            }

            var newEntity = new TableEntity
            {
                PartitionKey = companyName,
                RowKey = Guid.NewGuid().ToString(),
                ["CompanyName"] = companyName,
                ["Form"] = form,
                ["FilingDate"] = filingDate.ToShortDateString(),
                ["Url"] = url,
                ["Processed"] = false
            };

            await _tableClient.AddEntityAsync(newEntity);
            _logger.LogTrace("Tracked new document: {CompanyName} {Form} {FilingDate}", companyName, form, filingDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track document: {Url}", url);
        }
    }

    public async Task MarkProcessedAsync(string url)
    {
        if (_tableClient == null)
        {
            _logger.LogWarning("Azure Table Storage not initialized. Skipping processed update.");
            return;
        }

        try
        {
            string filter = $"Url eq '{url}'";
            var results = _tableClient.Query<TableEntity>(filter).ToList();
            
            if (results.Count > 0)
            {
                var entityToUpdate = results[0];
                entityToUpdate["Processed"] = true;
                await _tableClient.UpdateEntityAsync(entityToUpdate, Azure.ETag.All);
                _logger.LogTrace("Marked document as processed: {Url}", url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark document as processed: {Url}", url);
        }
    }

    public async Task<List<DocumentInfo>> GetUnprocessedAsync()
    {
        if (_tableClient == null)
        {
            _logger.LogWarning("Azure Table Storage not initialized. Returning empty list.");
            return new List<DocumentInfo>();
        }

        try
        {
            string filter = "Processed eq false";
            var results = _tableClient.Query<TableEntity>(filter).ToList();
            
            return results.Select(entity => new DocumentInfo
            {
                Id = entity.RowKey,
                CompanyName = entity.GetString("CompanyName"),
                Form = entity.GetString("Form"),
                FilingDate = DateTime.Parse(entity.GetString("FilingDate")),
                Url = entity.GetString("Url"),
                Processed = entity.GetBoolean("Processed") ?? false
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unprocessed documents");
            return new List<DocumentInfo>();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            if (_tableClient == null || string.IsNullOrEmpty(_config.AzureConnectionString))
                return false;

            // Try a simple operation to check connectivity
            await _tableClient.GetEntityAsync<TableEntity>("health", "check");
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Entity not found is expected and means the connection is working
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetStorageType() => "Azure Table Storage";
}
