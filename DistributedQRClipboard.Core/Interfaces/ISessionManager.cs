using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Core.Interfaces;

/// <summary>
/// Interface for managing sessions in the distributed QR clipboard system.
/// Provides secure session creation, validation, and device tracking.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Creates a new session with a cryptographically secure session ID.
    /// </summary>
    /// <param name="request">The session creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the created session information</returns>
    /// <exception cref="ArgumentException">Thrown when request is invalid</exception>
    Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Joins an existing session with the specified device.
    /// </summary>
    /// <param name="request">The join session request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A response containing the joined session information</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    /// <exception cref="InvalidSessionException">Thrown when session is expired or invalid</exception>
    /// <exception cref="DeviceOperationException">Thrown when device limit is exceeded</exception>
    Task<JoinSessionResponse> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session information if found</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    Task<SessionInfo> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a session exists and is active.
    /// </summary>
    /// <param name="sessionId">The session ID to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if session is valid and active, false otherwise</returns>
    Task<bool> ValidateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device from a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID to remove</param>
    /// <param name="reason">The reason for leaving</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated session information</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    Task<SessionInfo> LeaveSessionAsync(Guid sessionId, Guid deviceId, DeviceLeaveReason reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity timestamp for a device in a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    Task UpdateDeviceActivityAsync(Guid sessionId, Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all devices currently connected to a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of connected devices</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    Task<IReadOnlyList<DeviceInfo>> GetSessionDevicesAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the expiration time of a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="extensionMinutes">Additional minutes to extend</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated session information</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    /// <exception cref="ArgumentException">Thrown when extension is invalid</exception>
    Task<SessionInfo> ExtendSessionAsync(Guid sessionId, int extensionMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly closes a session and removes all devices.
    /// </summary>
    /// <param name="sessionId">The session ID to close</param>
    /// <param name="reason">The reason for closing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="SessionNotFoundException">Thrown when session is not found</exception>
    Task CloseSessionAsync(Guid sessionId, SessionEndReason reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup of expired sessions and inactive devices.
    /// This method should be called periodically by a background service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of sessions and devices cleaned up</returns>
    Task<(int ExpiredSessions, int InactiveDevices)> CleanupExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about all active sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session statistics</returns>
    Task<SessionStatistics> GetSessionStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about active sessions in the system.
/// </summary>
/// <param name="TotalActiveSessions">Total number of active sessions</param>
/// <param name="TotalConnectedDevices">Total number of connected devices across all sessions</param>
/// <param name="AverageDevicesPerSession">Average number of devices per session</param>
/// <param name="OldestSessionAge">Age of the oldest active session</param>
/// <param name="SessionsCreatedInLastHour">Number of sessions created in the last hour</param>
public readonly record struct SessionStatistics(
    int TotalActiveSessions,
    int TotalConnectedDevices,
    double AverageDevicesPerSession,
    TimeSpan OldestSessionAge,
    int SessionsCreatedInLastHour);

/// <summary>
/// Internal session data structure for tracking session state.
/// </summary>
internal readonly record struct SessionData(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime LastActivity,
    Dictionary<Guid, DeviceInfo> ConnectedDevices,
    int MaxDevices = 5)
{
    /// <summary>
    /// Converts to public SessionInfo model.
    /// </summary>
    public readonly SessionInfo ToSessionInfo() => new(
        SessionId,
        CreatedAt,
        ExpiresAt,
        ConnectedDevices.Count,
        LastActivity);

    /// <summary>
    /// Indicates whether the session is currently active.
    /// </summary>
    public readonly bool IsActive => DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Indicates whether the session can accept more devices.
    /// </summary>
    public readonly bool CanAcceptDevices => ConnectedDevices.Count < MaxDevices;

    /// <summary>
    /// Gets the remaining time until session expiration.
    /// </summary>
    public readonly TimeSpan RemainingTime => IsActive ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
}
