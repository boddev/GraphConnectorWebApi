using ApiGraphActivator.Models.Mcp;
using ApiGraphActivator.Services.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ApiGraphActivator.Tests;

/// <summary>
/// Simple tests for MCP session management functionality
/// </summary>
public static class McpSessionTests
{
    public static async Task RunAllTests()
    {
        Console.WriteLine("Running MCP Session Management Tests...");
        
        await TestSessionCreation();
        await TestSessionValidation();
        await TestSessionAuthentication();
        await TestConnectionManagement();
        await TestSessionCleanup();
        
        Console.WriteLine("All MCP tests completed successfully!");
    }

    private static async Task TestSessionCreation()
    {
        Console.WriteLine("Testing session creation...");
        
        var config = Options.Create(new SessionCleanupConfiguration());
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SessionManager>();
        var authProvider = new MockAuthProvider();
        
        var sessionManager = new SessionManager(logger, config, authProvider);
        
        var request = new CreateSessionRequest
        {
            ClientId = "test-client",
            UserId = "test-user",
            TenantId = "test-tenant",
            RequestedCapabilities = new List<string> { "edgar-search", "document-processing" }
        };
        
        var response = await sessionManager.CreateSessionAsync(request);
        
        if (!response.Success)
            throw new Exception($"Session creation failed: {response.ErrorMessage}");
        
        if (string.IsNullOrEmpty(response.SessionId))
            throw new Exception("Session ID should not be empty");
            
        if (response.GrantedCapabilities.Count != 2)
            throw new Exception("Should have granted 2 capabilities");
        
        Console.WriteLine($"✓ Session created successfully: {response.SessionId}");
    }

    private static async Task TestSessionValidation()
    {
        Console.WriteLine("Testing session validation...");
        
        var config = Options.Create(new SessionCleanupConfiguration());
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SessionManager>();
        var authProvider = new MockAuthProvider();
        
        var sessionManager = new SessionManager(logger, config, authProvider);
        
        // Create a session first
        var request = new CreateSessionRequest { ClientId = "test-client" };
        var createResponse = await sessionManager.CreateSessionAsync(request);
        
        // Validate the session
        var validation = await sessionManager.ValidateSessionAsync(createResponse.SessionId);
        
        if (!validation.IsValid)
            throw new Exception($"Session validation failed: {validation.ErrorMessage}");
        
        if (validation.Session == null)
            throw new Exception("Session should not be null");
        
        // Test invalid session ID
        var invalidValidation = await sessionManager.ValidateSessionAsync("invalid-session-id");
        if (invalidValidation.IsValid)
            throw new Exception("Invalid session should not be valid");
        
        Console.WriteLine("✓ Session validation working correctly");
    }

    private static async Task TestSessionAuthentication()
    {
        Console.WriteLine("Testing session authentication...");
        
        var config = Options.Create(new SessionCleanupConfiguration());
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SessionManager>();
        var authProvider = new MockAuthProvider();
        
        var sessionManager = new SessionManager(logger, config, authProvider);
        
        // Create a session
        var request = new CreateSessionRequest { ClientId = "test-client", TenantId = "test-tenant" };
        var createResponse = await sessionManager.CreateSessionAsync(request);
        
        // Test authentication
        var authInfo = await authProvider.AuthenticateWithAzureAdAsync("mock-token");
        if (authInfo == null || !authInfo.IsAuthenticated)
            throw new Exception("Authentication should succeed with mock token");
        
        // Set authentication info in session
        await sessionManager.SetSessionDataAsync(createResponse.SessionId, "authentication", authInfo);
        
        // Verify it was stored
        var storedAuth = await sessionManager.GetSessionDataAsync<McpAuthenticationInfo>(createResponse.SessionId, "authentication");
        if (storedAuth == null || !storedAuth.IsAuthenticated)
            throw new Exception("Authentication info should be stored and authenticated");
        
        Console.WriteLine("✓ Session authentication working correctly");
    }

    private static async Task TestConnectionManagement()
    {
        Console.WriteLine("Testing connection management...");
        
        var config = Options.Create(new SessionCleanupConfiguration());
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ConnectionManager>();
        
        var connectionManager = new ConnectionManager(logger, config);
        
        // Register a connection
        var connectionId = await connectionManager.RegisterConnectionAsync("test-session", "192.168.1.1:8080");
        
        if (string.IsNullOrEmpty(connectionId))
            throw new Exception("Connection ID should not be empty");
        
        // Get connection info
        var connection = await connectionManager.GetConnectionAsync(connectionId);
        if (connection == null)
            throw new Exception("Connection should exist");
        
        if (connection.State != ConnectionState.Connected)
            throw new Exception("Connection should be in Connected state");
        
        // Update activity
        await connectionManager.UpdateConnectionActivityAsync(connectionId);
        
        // Disconnect
        await connectionManager.DisconnectAsync(connectionId);
        
        Console.WriteLine("✓ Connection management working correctly");
    }

    private static async Task TestSessionCleanup()
    {
        Console.WriteLine("Testing session cleanup...");
        
        var config = Options.Create(new SessionCleanupConfiguration
        {
            InactivityTimeout = TimeSpan.FromMilliseconds(100) // Very short for testing
        });
        
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SessionManager>();
        var authProvider = new MockAuthProvider();
        
        var sessionManager = new SessionManager(logger, config, authProvider);
        
        // Create multiple sessions
        for (int i = 0; i < 3; i++)
        {
            var request = new CreateSessionRequest { ClientId = $"test-client-{i}" };
            await sessionManager.CreateSessionAsync(request);
        }
        
        var statsBefore = await sessionManager.GetSessionStatisticsAsync();
        if (statsBefore.TotalActiveSessions != 3)
            throw new Exception("Should have 3 active sessions");
        
        // Wait for sessions to become inactive
        await Task.Delay(200);
        
        // Run cleanup
        await sessionManager.CleanupExpiredSessionsAsync();
        
        var statsAfter = await sessionManager.GetSessionStatisticsAsync();
        if (statsAfter.TotalActiveSessions != 0)
            throw new Exception("All sessions should be cleaned up");
        
        Console.WriteLine("✓ Session cleanup working correctly");
    }
}

/// <summary>
/// Mock authentication provider for testing
/// </summary>
public class MockAuthProvider : ISessionAuthenticationProvider
{
    public async Task<McpAuthenticationInfo?> AuthenticateWithAzureAdAsync(string accessToken)
    {
        await Task.CompletedTask;
        
        if (accessToken == "mock-token")
        {
            return new McpAuthenticationInfo
            {
                AuthenticationType = "Mock",
                AccessToken = accessToken,
                TokenExpiresAt = DateTime.UtcNow.AddHours(1),
                IsAuthenticated = true,
                Claims = new Dictionary<string, string>
                {
                    ["sub"] = "mock-user-id",
                    ["name"] = "Mock User",
                    ["email"] = "mock@example.com"
                }
            };
        }
        
        return null;
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        await Task.CompletedTask;
        return token == "mock-token";
    }

    public async Task<McpAuthenticationInfo?> RefreshTokenAsync(string refreshToken)
    {
        await Task.CompletedTask;
        return null; // Not implemented for mock
    }

    public async Task<Dictionary<string, string>> ExtractClaimsAsync(string token)
    {
        await Task.CompletedTask;
        
        if (token == "mock-token")
        {
            return new Dictionary<string, string>
            {
                ["sub"] = "mock-user-id",
                ["name"] = "Mock User",
                ["email"] = "mock@example.com"
            };
        }
        
        return new Dictionary<string, string>();
    }
}