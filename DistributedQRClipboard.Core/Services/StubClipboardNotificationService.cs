using Microsoft.Extensions.Logging;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Services;

/// <summary>
/// Stub implementation of clipboard notification service for core functionality.
/// This will be replaced with SignalR implementation in later tasks.
/// </summary>
public sealed class StubClipboardNotificationService(
    ILogger<StubClipboardNotificationService> logger) : IClipboardNotificationService
{
    /// <inheritdoc />
    public async Task<int> NotifyClipboardUpdatedAsync(Guid sessionId, Guid excludeDeviceId, ClipboardUpdatedEvent clipboardEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Clipboard updated notification for session {SessionId}, excluding device {DeviceId}", 
            sessionId, excludeDeviceId);

        // Simulate async operation
        await Task.Delay(1, cancellationToken);
        
        // Return simulated device count
        return 1;
    }

    /// <inheritdoc />
    public async Task<int> NotifyClipboardClearedAsync(Guid sessionId, Guid excludeDeviceId, DeviceInfo clearedBy, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Clipboard cleared notification for session {SessionId}, cleared by {DeviceName}, excluding device {DeviceId}", 
            sessionId, clearedBy.DeviceName, excludeDeviceId);

        // Simulate async operation
        await Task.Delay(1, cancellationToken);
        
        // Return simulated device count
        return 1;
    }
}
