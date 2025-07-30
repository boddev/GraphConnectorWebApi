using ApiGraphActivator.Models.Mcp;
using Microsoft.Extensions.Options;

namespace ApiGraphActivator.Services.Mcp;

/// <summary>
/// Background service for cleaning up expired sessions and inactive connections
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly SessionCleanupConfiguration _config;

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger,
        IOptions<SessionCleanupConfiguration> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.EnableAutomaticCleanup)
        {
            _logger.LogInformation("Automatic session cleanup is disabled");
            return;
        }

        _logger.LogInformation("Session cleanup service started with interval {Interval}", _config.CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync();
                await Task.Delay(_config.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                
                // Wait a shorter time before retrying on error
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Session cleanup service stopped");
    }

    private async Task PerformCleanupAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionManager = scope.ServiceProvider.GetService<ISessionManager>();
            var connectionManager = scope.ServiceProvider.GetService<IConnectionManager>();

            if (sessionManager != null)
            {
                _logger.LogDebug("Starting session cleanup");
                await sessionManager.CleanupExpiredSessionsAsync();
            }

            if (connectionManager != null)
            {
                _logger.LogDebug("Starting connection cleanup");
                await connectionManager.CleanupInactiveConnectionsAsync();
            }

            _logger.LogTrace("Cleanup cycle completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing cleanup");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session cleanup service is stopping");
        await base.StopAsync(cancellationToken);
    }
}