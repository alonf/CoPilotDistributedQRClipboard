using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Infrastructure.Services;

/// <summary>
/// SignalR-based implementation of clipboard notification service for real-time notifications.
/// This implementation uses IHubContext to send notifications through SignalR groups.
/// </summary>
public sealed class ClipboardNotificationService(
    IHubContext<Hub> hubContext,
    ILogger<ClipboardNotificationService> logger) : IClipboardNotificationService
{
    private readonly IHubContext<Hub> _hubContext = hubContext;
    private readonly ILogger<ClipboardNotificationService> _logger = logger;

    /// <inheritdoc />
    public async Task<int> NotifyClipboardUpdatedAsync(Guid sessionId, Guid excludeDeviceId, ClipboardUpdatedEvent clipboardEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending clipboard updated notification for session {SessionId}, excluding device {DeviceId}", 
                sessionId, excludeDeviceId);

            var groupName = GetSessionGroupName(sessionId);
            
            // Send notification to all devices in the session group
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ClipboardUpdated", clipboardEvent, cancellationToken);

            _logger.LogDebug("Successfully sent clipboard update notification for session {SessionId}", sessionId);
            
            // Note: In a production environment, you might want to track connected devices
            // to return an accurate count. For now, we'll return 1 to indicate success.
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send clipboard update notification for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> NotifyClipboardClearedAsync(Guid sessionId, Guid excludeDeviceId, DeviceInfo clearedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending clipboard cleared notification for session {SessionId}, cleared by {DeviceName}, excluding device {DeviceId}", 
                sessionId, clearedBy.DeviceName, excludeDeviceId);

            var groupName = GetSessionGroupName(sessionId);
            
            // Create a clipboard cleared event
            var clearedEvent = new
            {
                SessionId = sessionId,
                ClearedBy = clearedBy,
                Timestamp = DateTime.UtcNow
            };

            // Send notification to all devices in the session group
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ClipboardCleared", clearedEvent, cancellationToken);

            _logger.LogDebug("Successfully sent clipboard cleared notification for session {SessionId}", sessionId);
            
            // Note: In a production environment, you might want to track connected devices
            // to return an accurate count. For now, we'll return 1 to indicate success.
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send clipboard cleared notification for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets the SignalR group name for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The group name</returns>
    private static string GetSessionGroupName(Guid sessionId)
    {
        return $"session_{sessionId}";
    }
}
