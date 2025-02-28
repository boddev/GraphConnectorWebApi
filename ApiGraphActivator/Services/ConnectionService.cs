// Define a static class named ConnectionService
static class ConnectionService
{
  // Define an asynchronous static method named CreateConnection
  async static Task CreateConnection()
  {
    // Output a message to the console indicating the start of the connection creation process
    Console.Write("Creating connection...");

    // Await the asynchronous operation of posting a new connection to the GraphService client
    await GraphService.Client.External.Connections
      .PostAsync(ConnectionConfiguration.ExternalConnection);

    // Output a message to the console indicating the completion of the connection creation process
    Console.WriteLine("DONE");
  }

  // Define an asynchronous static method named CreateSchema
  async static Task CreateSchema()
  {
    // Output a message to the console indicating the start of the schema creation process
    Console.WriteLine("Creating schema...");

    // Await the asynchronous operation of patching the schema for the specified connection in the GraphService client
    await GraphService.Client.External
      .Connections[ConnectionConfiguration.ExternalConnection.Id]
      .Schema
      .PatchAsync(ConnectionConfiguration.Schema);

    // Output a message to the console indicating the completion of the schema creation process
    Console.WriteLine("DONE");
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
      Console.WriteLine(ex.Message);
    }
  }
}