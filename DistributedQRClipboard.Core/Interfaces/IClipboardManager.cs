using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Interfaces;

/// <summary>
/// Manages clipboard content for sessions with validation, storage, and real-time synchronization.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Copies content to the shared clipboard for a session.
    /// </summary>
    /// <param name="request">The copy request containing session ID, device ID, and content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The copy response indicating success/failure and metadata</returns>
    /// <exception cref="SessionNotFoundException">Thrown when the session doesn't exist</exception>
    /// <exception cref="InvalidSessionException">Thrown when the session is invalid or expired</exception>
    /// <exception cref="ClipboardValidationException">Thrown when content validation fails</exception>
    /// <exception cref="DeviceOperationException">Thrown when device is not authorized</exception>
    Task<CopyToClipboardResponse> CopyToClipboardAsync(CopyToClipboardRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest clipboard content for a session.
    /// </summary>
    /// <param name="request">The get request containing session ID and device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The clipboard content response</returns>
    /// <exception cref="SessionNotFoundException">Thrown when the session doesn't exist</exception>
    /// <exception cref="InvalidSessionException">Thrown when the session is invalid or expired</exception>
    /// <exception cref="DeviceOperationException">Thrown when device is not authorized</exception>
    Task<GetClipboardResponse> GetClipboardAsync(GetClipboardRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the clipboard history for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID requesting the history</param>
    /// <param name="limit">Maximum number of entries to return (default: 10, max: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of clipboard history entries</returns>
    /// <exception cref="SessionNotFoundException">Thrown when the session doesn't exist</exception>
    /// <exception cref="InvalidSessionException">Thrown when the session is invalid or expired</exception>
    /// <exception cref="DeviceOperationException">Thrown when device is not authorized</exception>
    Task<IReadOnlyList<ClipboardHistoryEntry>> GetClipboardHistoryAsync(Guid sessionId, Guid deviceId, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the clipboard content for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID requesting the clear operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cleared successfully</returns>
    /// <exception cref="SessionNotFoundException">Thrown when the session doesn't exist</exception>
    /// <exception cref="InvalidSessionException">Thrown when the session is invalid or expired</exception>
    /// <exception cref="DeviceOperationException">Thrown when device is not authorized</exception>
    Task<bool> ClearClipboardAsync(Guid sessionId, Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates clipboard content without storing it.
    /// </summary>
    /// <param name="content">The content to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with details</returns>
    Task<ContentValidationResult> ValidateContentAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clipboard statistics for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Clipboard statistics</returns>
    /// <exception cref="SessionNotFoundException">Thrown when the session doesn't exist</exception>
    Task<ClipboardStatistics> GetClipboardStatisticsAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time notification service for clipboard events.
/// </summary>
public interface IClipboardNotificationService
{
    /// <summary>
    /// Notifies all devices in a session about clipboard content update.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="excludeDeviceId">Device ID to exclude from notification (typically the sender)</param>
    /// <param name="clipboardEvent">The clipboard update event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of devices notified</returns>
    Task<int> NotifyClipboardUpdatedAsync(Guid sessionId, Guid excludeDeviceId, ClipboardUpdatedEvent clipboardEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all devices in a session about clipboard being cleared.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="excludeDeviceId">Device ID to exclude from notification</param>
    /// <param name="clearedBy">Information about who cleared the clipboard</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of devices notified</returns>
    Task<int> NotifyClipboardClearedAsync(Guid sessionId, Guid excludeDeviceId, DeviceInfo clearedBy, CancellationToken cancellationToken = default);
}
