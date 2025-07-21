using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Services;

namespace DistributedQRClipboard.Infrastructure.Services;

/// <summary>
/// Background service that periodically cleans up expired sessions and inactive devices.
/// </summary>
public sealed class SessionCleanupService(
    IServiceProvider serviceProvider,
    IOptions<SessionManagerOptions> options,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private readonly SessionManagerOptions _options = options.Value;

    /// <summary>
    /// Executes the background service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Session cleanup service started. Cleanup interval: {IntervalMinutes} minutes", 
            _options.CleanupIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
                
                // Wait for the next cleanup interval
                var delay = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped
                logger.LogInformation("Session cleanup service is stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during session cleanup");
                
                // Wait a shorter time before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        logger.LogInformation("Session cleanup service stopped");
    }

    /// <summary>
    /// Performs the actual cleanup operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();

        try
        {
            var (expiredSessions, inactiveDevices) = await sessionManager.CleanupExpiredAsync(cancellationToken);

            if (expiredSessions > 0 || inactiveDevices > 0)
            {
                logger.LogInformation("Cleanup completed: {ExpiredSessions} expired sessions, {InactiveDevices} inactive devices removed",
                    expiredSessions, inactiveDevices);
            }
            else
            {
                logger.LogDebug("Cleanup completed: No expired sessions or inactive devices found");
            }

            // Log session statistics periodically
            var stats = await sessionManager.GetSessionStatisticsAsync(cancellationToken);
            logger.LogDebug("Current session statistics: {ActiveSessions} active sessions, {TotalDevices} total devices, {SessionsCreatedLastHour} sessions created in last hour",
                stats.TotalActiveSessions, stats.TotalConnectedDevices, stats.SessionsCreatedInLastHour);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during session cleanup operation");
            throw;
        }
    }

    /// <summary>
    /// Called when the service is stopping.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Session cleanup service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
