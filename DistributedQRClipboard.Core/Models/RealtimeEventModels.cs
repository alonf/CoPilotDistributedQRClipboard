namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Base interface for all real-time events in the system.
/// </summary>
public interface IRealtimeEvent
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Session ID where the event occurred.
    /// </summary>
    Guid SessionId { get; }

    /// <summary>
    /// Correlation ID for tracking related events.
    /// </summary>
    string CorrelationId { get; }
}

/// <summary>
/// Event fired when clipboard content is updated in a session.
/// </summary>
/// <param name="EventId">Unique event identifier</param>
/// <param name="Timestamp">UTC timestamp of the event</param>
/// <param name="SessionId">Session where clipboard was updated</param>
/// <param name="CorrelationId">Correlation ID for tracking</param>
/// <param name="Content">The new clipboard content</param>
/// <param name="UpdatedByDeviceId">Device that updated the clipboard</param>
/// <param name="PreviousContentHash">Hash of previous content (for comparison)</param>
public readonly record struct ClipboardUpdatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SessionId,
    string CorrelationId,
    ClipboardContent Content,
    Guid UpdatedByDeviceId,
    string? PreviousContentHash = null) : IRealtimeEvent
{
    /// <summary>
    /// Creates a new clipboard updated event.
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="content">Updated content</param>
    /// <param name="deviceId">Device that made the update</param>
    /// <param name="previousHash">Previous content hash</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>New ClipboardUpdatedEvent</returns>
    public static ClipboardUpdatedEvent Create(
        Guid sessionId,
        ClipboardContent content,
        Guid deviceId,
        string? previousHash = null,
        string? correlationId = null)
    {
        return new ClipboardUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            sessionId,
            correlationId ?? Guid.NewGuid().ToString(),
            content,
            deviceId,
            previousHash);
    }
}

/// <summary>
/// Event fired when a device joins a session.
/// </summary>
/// <param name="EventId">Unique event identifier</param>
/// <param name="Timestamp">UTC timestamp of the event</param>
/// <param name="SessionId">Session that was joined</param>
/// <param name="CorrelationId">Correlation ID for tracking</param>
/// <param name="Device">Information about the device that joined</param>
/// <param name="TotalDeviceCount">Total number of devices in session after join</param>
public readonly record struct DeviceJoinedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SessionId,
    string CorrelationId,
    DeviceInfo Device,
    int TotalDeviceCount) : IRealtimeEvent
{
    /// <summary>
    /// Creates a new device joined event.
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="device">Device information</param>
    /// <param name="totalDeviceCount">Total devices in session</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>New DeviceJoinedEvent</returns>
    public static DeviceJoinedEvent Create(
        Guid sessionId,
        DeviceInfo device,
        int totalDeviceCount,
        string? correlationId = null)
    {
        return new DeviceJoinedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            sessionId,
            correlationId ?? Guid.NewGuid().ToString(),
            device,
            totalDeviceCount);
    }
}

/// <summary>
/// Event fired when a device leaves a session.
/// </summary>
/// <param name="EventId">Unique event identifier</param>
/// <param name="Timestamp">UTC timestamp of the event</param>
/// <param name="SessionId">Session that was left</param>
/// <param name="CorrelationId">Correlation ID for tracking</param>
/// <param name="DeviceId">ID of the device that left</param>
/// <param name="DeviceName">Name of the device that left</param>
/// <param name="TotalDeviceCount">Total number of devices in session after leave</param>
/// <param name="Reason">Reason for leaving (timeout, explicit disconnect, etc.)</param>
public readonly record struct DeviceLeftEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SessionId,
    string CorrelationId,
    Guid DeviceId,
    string? DeviceName,
    int TotalDeviceCount,
    DeviceLeaveReason Reason) : IRealtimeEvent
{
    /// <summary>
    /// Creates a new device left event.
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <param name="deviceName">Device name</param>
    /// <param name="totalDeviceCount">Total devices in session</param>
    /// <param name="reason">Reason for leaving</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>New DeviceLeftEvent</returns>
    public static DeviceLeftEvent Create(
        Guid sessionId,
        Guid deviceId,
        string? deviceName,
        int totalDeviceCount,
        DeviceLeaveReason reason,
        string? correlationId = null)
    {
        return new DeviceLeftEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            sessionId,
            correlationId ?? Guid.NewGuid().ToString(),
            deviceId,
            deviceName,
            totalDeviceCount,
            reason);
    }
}

/// <summary>
/// Event fired when a session expires or is closed.
/// </summary>
/// <param name="EventId">Unique event identifier</param>
/// <param name="Timestamp">UTC timestamp of the event</param>
/// <param name="SessionId">Session that expired/closed</param>
/// <param name="CorrelationId">Correlation ID for tracking</param>
/// <param name="Reason">Reason for session ending</param>
/// <param name="FinalDeviceCount">Number of devices when session ended</param>
public readonly record struct SessionEndedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SessionId,
    string CorrelationId,
    SessionEndReason Reason,
    int FinalDeviceCount) : IRealtimeEvent
{
    /// <summary>
    /// Creates a new session ended event.
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="reason">Reason for ending</param>
    /// <param name="deviceCount">Final device count</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>New SessionEndedEvent</returns>
    public static SessionEndedEvent Create(
        Guid sessionId,
        SessionEndReason reason,
        int deviceCount,
        string? correlationId = null)
    {
        return new SessionEndedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            sessionId,
            correlationId ?? Guid.NewGuid().ToString(),
            reason,
            deviceCount);
    }
}

/// <summary>
/// Reasons why a device might leave a session.
/// </summary>
public enum DeviceLeaveReason
{
    /// <summary>
    /// Device explicitly disconnected.
    /// </summary>
    Disconnect,

    /// <summary>
    /// Device timed out due to inactivity.
    /// </summary>
    Timeout,

    /// <summary>
    /// Session expired.
    /// </summary>
    SessionExpired,

    /// <summary>
    /// Connection error occurred.
    /// </summary>
    ConnectionError,

    /// <summary>
    /// Server shutdown or maintenance.
    /// </summary>
    ServerShutdown
}

/// <summary>
/// Reasons why a session might end.
/// </summary>
public enum SessionEndReason
{
    /// <summary>
    /// Session expired due to time limit.
    /// </summary>
    Expired,

    /// <summary>
    /// All devices left the session.
    /// </summary>
    AllDevicesLeft,

    /// <summary>
    /// Session was explicitly closed.
    /// </summary>
    ExplicitClose,

    /// <summary>
    /// Server shutdown or maintenance.
    /// </summary>
    ServerShutdown,

    /// <summary>
    /// Session exceeded resource limits.
    /// </summary>
    ResourceLimitExceeded
}
