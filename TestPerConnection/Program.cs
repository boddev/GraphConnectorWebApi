using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApiGraphActivator.Services;

namespace ApiGraphActivator.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing per-connection document tracking...");
            
            // Create a logger factory
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger<LocalFileStorageService>();
            
            // Create storage configuration
            var storageConfig = new StorageConfiguration
            {
                LocalDataPath = @"c:\temp\test-data",
                AutoCreateTables = true
            };
            
            // Create service
            var storageService = new LocalFileStorageService(logger, storageConfig);
            
            try
            {
                await storageService.InitializeAsync();
                
                // Test 1: Add document to connection-1
                Console.WriteLine("\n=== Test 1: Adding document to connection-1 ===");
                await storageService.TrackDocumentAsync("Apple Inc.", "10-K", DateTime.Now, "https://test.com/doc1", "connection-1");
                
                // Test 2: Add document to connection-2
                Console.WriteLine("\n=== Test 2: Adding document to connection-2 ===");
                await storageService.TrackDocumentAsync("Microsoft Corp.", "10-Q", DateTime.Now, "https://test.com/doc2", "connection-2");
                
                // Test 3: Add document to global (no connectionId)
                Console.WriteLine("\n=== Test 3: Adding document to global ===");
                await storageService.TrackDocumentAsync("Google Inc.", "8-K", DateTime.Now, "https://test.com/doc3", null);
                
                // Check files created
                Console.WriteLine("\n=== Checking files created ===");
                var dataDir = new DirectoryInfo(storageConfig.LocalDataPath);
                if (dataDir.Exists)
                {
                    foreach (var file in dataDir.GetFiles("*.json"))
                    {
                        Console.WriteLine($"  File: {file.Name}");
                        var content = await File.ReadAllTextAsync(file.FullName);
                        Console.WriteLine($"    Content length: {content.Length} characters");
                        Console.WriteLine($"    First 200 chars: {content.Substring(0, Math.Min(200, content.Length))}...");
                        Console.WriteLine();
                    }
                }
                
                // Test getting unprocessed documents per connection
                Console.WriteLine("\n=== Test 4: Getting unprocessed documents per connection ===");
                var conn1Docs = await storageService.GetUnprocessedAsync("connection-1");
                var conn2Docs = await storageService.GetUnprocessedAsync("connection-2");
                var globalDocs = await storageService.GetUnprocessedAsync(null);
                
                Console.WriteLine($"Connection-1 documents: {conn1Docs.Count}");
                foreach (var doc in conn1Docs)
                {
                    Console.WriteLine($"  {doc.CompanyName}: {doc.Form} (ConnectionId: '{doc.ConnectionId}')");
                }
                
                Console.WriteLine($"Connection-2 documents: {conn2Docs.Count}");
                foreach (var doc in conn2Docs)
                {
                    Console.WriteLine($"  {doc.CompanyName}: {doc.Form} (ConnectionId: '{doc.ConnectionId}')");
                }
                
                Console.WriteLine($"Global documents: {globalDocs.Count}");
                foreach (var doc in globalDocs)
                {
                    Console.WriteLine($"  {doc.CompanyName}: {doc.Form} (ConnectionId: '{doc.ConnectionId}')");
                }
                
                Console.WriteLine("\n✅ Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
