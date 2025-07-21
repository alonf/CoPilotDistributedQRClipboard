namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Represents immutable session information using C# 13 record type.
/// </summary>
/// <param name="SessionId">Unique session identifier (GUID)</param>
/// <param name="CreatedAt">UTC timestamp when the session was created</param>
/// <param name="ExpiresAt">UTC timestamp when the session expires</param>
/// <param name="DeviceCount">Current number of connected devices</param>
/// <param name="LastActivity">UTC timestamp of last activity in the session</param>
public readonly record struct SessionInfo(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    int DeviceCount,
    DateTime LastActivity)
{
    /// <summary>
    /// Indicates whether the session is currently active (not expired).
    /// </summary>
    public readonly bool IsActive => DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Gets the remaining time until session expiration.
    /// </summary>
    public readonly TimeSpan RemainingTime => IsActive ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
}

/// <summary>
/// Request model for creating a new session.
/// </summary>
/// <param name="DeviceName">Optional name for the device creating the session</param>
/// <param name="ExpirationMinutes">Session expiration time in minutes (default: 60)</param>
public readonly record struct CreateSessionRequest(
    string? DeviceName = null,
    int ExpirationMinutes = 60)
{
    /// <summary>
    /// Validates the expiration minutes value.
    /// </summary>
    public readonly bool IsValid => ExpirationMinutes is > 0 and <= 1440; // Max 24 hours
}

/// <summary>
/// Response model for session creation.
/// </summary>
/// <param name="SessionInfo">The created session information</param>
/// <param name="QrCodeUrl">URL that can be used to join the session</param>
/// <param name="QrCodeBase64">Base64-encoded QR code image</param>
/// <param name="Success">Indicates whether the operation was successful</param>
/// <param name="ErrorMessage">Error message if operation failed</param>
public readonly record struct CreateSessionResponse(
    SessionInfo? SessionInfo,
    string? QrCodeUrl,
    string? QrCodeBase64,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Request model for joining an existing session.
/// </summary>
/// <param name="SessionId">The session ID to join</param>
/// <param name="DeviceId">The device ID of the joining device</param>
/// <param name="DeviceName">Optional name for the joining device</param>
public readonly record struct JoinSessionRequest(
    Guid SessionId,
    Guid DeviceId,
    string? DeviceName = null);

/// <summary>
/// Response model for joining a session.
/// </summary>
/// <param name="SessionInfo">The joined session information</param>
/// <param name="Success">Indicates whether the operation was successful</param>
/// <param name="ErrorMessage">Error message if operation failed</param>
public readonly record struct JoinSessionResponse(
    SessionInfo? SessionInfo,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Represents a device connected to a session.
/// </summary>
/// <param name="DeviceId">Unique device identifier</param>
/// <param name="DeviceName">Optional device name</param>
/// <param name="JoinedAt">UTC timestamp when device joined</param>
/// <param name="LastSeen">UTC timestamp of last activity</param>
public readonly record struct DeviceInfo(
    Guid DeviceId,
    string? DeviceName,
    DateTime JoinedAt,
    DateTime LastSeen)
{
    /// <summary>
    /// Indicates whether the device is considered active (seen within last 5 minutes).
    /// </summary>
    public readonly bool IsActive => DateTime.UtcNow - LastSeen < TimeSpan.FromMinutes(5);
}
