// Define a static class named ConnectionService
using Microsoft.Graph.Models.ExternalConnectors;

static class ConnectionService
{
  public static ILogger logger;
  // Define an asynchronous static method named CreateConnection
  async static Task<(bool Success, ExternalConnection? Result, string ErrorMessage)> CreateConnection()
  {
    try
    {
        ExternalConnection connection = ConnectionConfiguration.ExternalConnection;

        // Await the asynchronous operation of posting a new connection to the GraphService client
        var result = await GraphService.Client.External.Connections.PostAsync(connection);
        
        logger.LogInformation($"Connection created successfully with ID: {result?.Id}");
        logger.LogInformation("DONE");
        
        return (true, result, string.Empty);
    }
    catch (Microsoft.Graph.Models.ODataErrors.ODataError odataError)
    {
        string errorDetails = $"Error code: {odataError.Error?.Code}, Message: {odataError.Error?.Message}";
        logger.LogError($"Failed to create connection: {errorDetails}");
        return (false, null, errorDetails);
    }
    catch (Exception ex)
    {
        logger.LogError($"Unexpected error creating connection: {ex.Message}");
        return (false, null, ex.Message);
    }
  }

  // Define an asynchronous static method named CreateSchema
  async static Task CreateSchema()
  {
    // Output a message to the console indicating the start of the schema creation process
    logger.LogInformation("Creating schema...");

    // Await the asynchronous operation of patching the schema for the specified connection in the GraphService client
    Schema schema = ConnectionConfiguration.Schema;
    await GraphService.Client.External
      .Connections[ConnectionConfiguration.ExternalConnection.Id]
      .Schema
      .PatchAsync(schema);

    // Output a message to the console indicating the completion of the schema creation process
    logger.LogInformation("DONE");
  }

  // Define a public asynchronous static method named ProvisionConnection
  public static async Task ProvisionConnection()
  {
    try
    {
      // Attempt to create a connection by calling the CreateConnection method
      await CreateConnection();
      // Attempt to create a schema by calling the CreateSchema method
      await CreateSchema();
    }
    catch (Exception ex)
    {
      // Catch any exceptions that occur during the connection or schema creation process and output the exception message to the console
      logger.LogError(ex.Message);
    }
  }

  public static void InitializeLogger(ILogger log)
  {
    logger = log;
  }
}