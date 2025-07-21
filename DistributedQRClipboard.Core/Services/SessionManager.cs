using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Core.Services;

/// <summary>
/// Configuration options for the session manager.
/// </summary>
public sealed class SessionManagerOptions
{
    /// <summary>
    /// Default session expiration time in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    /// Maximum number of devices per session.
    /// </summary>
    public int MaxDevicesPerSession { get; set; } = 5;

    /// <summary>
    /// Maximum number of concurrent sessions.
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 1000;

    /// <summary>
    /// Device inactivity timeout in minutes.
    /// </summary>
    public int DeviceInactivityTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Session ID length in characters (minimum 32 for security).
    /// </summary>
    public int SessionIdLength { get; set; } = 64;

    /// <summary>
    /// Cleanup interval in minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// Manages sessions in the distributed QR clipboard system with cryptographically secure session IDs.
/// Implements secure session creation, validation, device tracking, and automatic cleanup.
/// </summary>
public sealed class SessionManager(
    IMemoryCache cache,
    IOptions<SessionManagerOptions> options,
    ILogger<SessionManager> logger) : ISessionManager
{
    private readonly SessionManagerOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, object> _sessionLocks = new();
    private readonly object _statsLock = new();

    // Statistics tracking
    private volatile int _sessionsCreatedInLastHour = 0;
    private DateTime _lastHourReset = DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating new session with device name: {DeviceName}", request.DeviceName);

        try
        {
            // Validate request
            if (!request.IsValid)
            {
                var errorMessage = $"Invalid session creation request. Expiration minutes must be between 1 and {_options.DefaultExpirationMinutes}.";
                logger.LogWarning("Session creation failed: {Error}", errorMessage);
                return new CreateSessionResponse(null, null, null, false, errorMessage);
            }

            // Check concurrent session limit
            var currentSessionCount = await GetActiveSessionCountAsync(cancellationToken);
            if (currentSessionCount >= _options.MaxConcurrentSessions)
            {
                var errorMessage = $"Maximum concurrent sessions limit ({_options.MaxConcurrentSessions}) reached.";
                logger.LogWarning("Session creation failed: {Error}", errorMessage);
                return new CreateSessionResponse(null, null, null, false, errorMessage);
            }

            // Generate cryptographically secure session ID
            var sessionId = GenerateSecureSessionId();
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var expirationMinutes = request.ExpirationMinutes > 0 ? request.ExpirationMinutes : _options.DefaultExpirationMinutes;
            var expiresAt = now.AddMinutes(expirationMinutes);

            // Create initial device
            var device = new DeviceInfo(deviceId, request.DeviceName, now, now);
            var devices = new Dictionary<Guid, DeviceInfo> { { deviceId, device } };

            // Create session data
            var sessionData = new SessionData(
                sessionId,
                now,
                expiresAt,
                now,
                devices,
                _options.MaxDevicesPerSession);

            // Store in cache with expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt,
                SlidingExpiration = TimeSpan.FromMinutes(_options.DeviceInactivityTimeoutMinutes),
                Priority = CacheItemPriority.Normal
            };

            cache.Set(GetSessionCacheKey(sessionId), sessionData, cacheOptions);

            // Update statistics
            UpdateSessionCreationStats();

            var sessionInfo = sessionData.ToSessionInfo();
            var qrCodeUrl = GenerateQrCodeUrl(sessionId);

            logger.LogInformation("Session created successfully: {SessionId} with device {DeviceId}", sessionId, deviceId);

            return new CreateSessionResponse(sessionInfo, qrCodeUrl, null, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating session");
            return new CreateSessionResponse(null, null, null, false, "An unexpected error occurred while creating the session.");
        }
    }

    /// <inheritdoc />
    public async Task<JoinSessionResponse> JoinSessionAsync(JoinSessionRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Device attempting to join session: {SessionId}", request.SessionId);

        try
        {
            var sessionData = await GetSessionDataAsync(request.SessionId, cancellationToken);
            
            // Validate session state
            if (!sessionData.IsActive)
            {
                throw new InvalidSessionException(request.SessionId, SessionInvalidReason.Expired);
            }

            if (!sessionData.CanAcceptDevices)
            {
                throw new InvalidSessionException(request.SessionId, SessionInvalidReason.MaxCapacityReached);
            }

            // Use session-specific lock to prevent race conditions
            var lockObject = _sessionLocks.GetOrAdd(request.SessionId, _ => new object());
            
            return await Task.Run(() =>
            {
                lock (lockObject)
                {
                    // Re-fetch session data under lock
                    if (!cache.TryGetValue(GetSessionCacheKey(request.SessionId), out SessionData lockedSessionData))
                    {
                        throw new SessionNotFoundException(request.SessionId);
                    }

                    // Re-validate under lock
                    if (!lockedSessionData.CanAcceptDevices)
                    {
                        throw new InvalidSessionException(request.SessionId, SessionInvalidReason.MaxCapacityReached);
                    }

                    // Create new device
                    var deviceId = Guid.NewGuid();
                    var now = DateTime.UtcNow;
                    var device = new DeviceInfo(deviceId, request.DeviceName, now, now);

                    // Add device to session
                    var updatedDevices = new Dictionary<Guid, DeviceInfo>(lockedSessionData.ConnectedDevices)
                    {
                        { deviceId, device }
                    };

                    var updatedSessionData = lockedSessionData with
                    {
                        ConnectedDevices = updatedDevices,
                        LastActivity = now
                    };

                    // Update cache
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = updatedSessionData.ExpiresAt,
                        SlidingExpiration = TimeSpan.FromMinutes(_options.DeviceInactivityTimeoutMinutes),
                        Priority = CacheItemPriority.Normal
                    };

                    cache.Set(GetSessionCacheKey(request.SessionId), updatedSessionData, cacheOptions);

                    var sessionInfo = updatedSessionData.ToSessionInfo();

                    logger.LogInformation("Device {DeviceId} joined session {SessionId}. Total devices: {DeviceCount}", 
                        deviceId, request.SessionId, updatedSessionData.ConnectedDevices.Count);

                    return new JoinSessionResponse(sessionInfo, true);
                }
            }, cancellationToken);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Attempt to join non-existent session: {SessionId}", request.SessionId);
            return new JoinSessionResponse(null, false, "Session not found or has expired.");
        }
        catch (InvalidSessionException ex)
        {
            logger.LogWarning("Attempt to join invalid session: {SessionId}, Reason: {Reason}", request.SessionId, ex.Reason);
            var errorMessage = ex.Reason switch
            {
                SessionInvalidReason.Expired => "Session has expired.",
                SessionInvalidReason.MaxCapacityReached => $"Session is full. Maximum {_options.MaxDevicesPerSession} devices allowed.",
                SessionInvalidReason.Closed => "Session has been closed.",
                _ => "Session is not available."
            };
            return new JoinSessionResponse(null, false, errorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error joining session {SessionId}", request.SessionId);
            return new JoinSessionResponse(null, false, "An unexpected error occurred while joining the session.");
        }
    }

    /// <inheritdoc />
    public async Task<SessionInfo> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving session information: {SessionId}", sessionId);

        var sessionData = await GetSessionDataAsync(sessionId, cancellationToken);
        return sessionData.ToSessionInfo();
    }

    /// <inheritdoc />
    public async Task<bool> ValidateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Validating session: {SessionId}", sessionId);

        try
        {
            var sessionData = await GetSessionDataAsync(sessionId, cancellationToken);
            return sessionData.IsActive;
        }
        catch (SessionNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<SessionInfo> LeaveSessionAsync(Guid sessionId, Guid deviceId, DeviceLeaveReason reason, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Device {DeviceId} leaving session {SessionId}, Reason: {Reason}", deviceId, sessionId, reason);

        var lockObject = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        return await Task.Run(() =>
        {
            lock (lockObject)
            {
                if (!cache.TryGetValue(GetSessionCacheKey(sessionId), out SessionData sessionData))
                {
                    throw new SessionNotFoundException(sessionId);
                }

                if (!sessionData.ConnectedDevices.ContainsKey(deviceId))
                {
                    logger.LogWarning("Device {DeviceId} not found in session {SessionId}", deviceId, sessionId);
                    return sessionData.ToSessionInfo();
                }

                // Remove device
                var updatedDevices = new Dictionary<Guid, DeviceInfo>(sessionData.ConnectedDevices);
                updatedDevices.Remove(deviceId);

                var now = DateTime.UtcNow;
                var updatedSessionData = sessionData with
                {
                    ConnectedDevices = updatedDevices,
                    LastActivity = now
                };

                // If no devices left, close session
                if (updatedDevices.Count == 0)
                {
                    cache.Remove(GetSessionCacheKey(sessionId));
                    _sessionLocks.TryRemove(sessionId, out _);
                    logger.LogInformation("Session {SessionId} closed - no devices remaining", sessionId);
                }
                else
                {
                    // Update cache
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = updatedSessionData.ExpiresAt,
                        SlidingExpiration = TimeSpan.FromMinutes(_options.DeviceInactivityTimeoutMinutes),
                        Priority = CacheItemPriority.Normal
                    };

                    cache.Set(GetSessionCacheKey(sessionId), updatedSessionData, cacheOptions);
                }

                logger.LogInformation("Device {DeviceId} left session {SessionId}. Remaining devices: {DeviceCount}", 
                    deviceId, sessionId, updatedDevices.Count);

                return updatedSessionData.ToSessionInfo();
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateDeviceActivityAsync(Guid sessionId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Updating device activity: {DeviceId} in session {SessionId}", deviceId, sessionId);

        var lockObject = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        await Task.Run(() =>
        {
            lock (lockObject)
            {
                if (!cache.TryGetValue(GetSessionCacheKey(sessionId), out SessionData sessionData))
                {
                    throw new SessionNotFoundException(sessionId);
                }

                if (!sessionData.ConnectedDevices.TryGetValue(deviceId, out var device))
                {
                    logger.LogWarning("Device {DeviceId} not found in session {SessionId}", deviceId, sessionId);
                    return;
                }

                var now = DateTime.UtcNow;
                var updatedDevice = device with { LastSeen = now };
                var updatedDevices = new Dictionary<Guid, DeviceInfo>(sessionData.ConnectedDevices)
                {
                    [deviceId] = updatedDevice
                };

                var updatedSessionData = sessionData with
                {
                    ConnectedDevices = updatedDevices,
                    LastActivity = now
                };

                // Update cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = updatedSessionData.ExpiresAt,
                    SlidingExpiration = TimeSpan.FromMinutes(_options.DeviceInactivityTimeoutMinutes),
                    Priority = CacheItemPriority.Normal
                };

                cache.Set(GetSessionCacheKey(sessionId), updatedSessionData, cacheOptions);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceInfo>> GetSessionDevicesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving devices for session: {SessionId}", sessionId);

        var sessionData = await GetSessionDataAsync(sessionId, cancellationToken);
        return sessionData.ConnectedDevices.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<SessionInfo> ExtendSessionAsync(Guid sessionId, int extensionMinutes, CancellationToken cancellationToken = default)
    {
        if (extensionMinutes <= 0 || extensionMinutes > _options.DefaultExpirationMinutes)
        {
            throw new ArgumentException($"Extension minutes must be between 1 and {_options.DefaultExpirationMinutes}.", nameof(extensionMinutes));
        }

        logger.LogInformation("Extending session {SessionId} by {ExtensionMinutes} minutes", sessionId, extensionMinutes);

        var lockObject = _sessionLocks.GetOrAdd(sessionId, _ => new object());

        return await Task.Run(() =>
        {
            lock (lockObject)
            {
                if (!cache.TryGetValue(GetSessionCacheKey(sessionId), out SessionData sessionData))
                {
                    throw new SessionNotFoundException(sessionId);
                }

                var newExpiresAt = sessionData.ExpiresAt.AddMinutes(extensionMinutes);
                var updatedSessionData = sessionData with
                {
                    ExpiresAt = newExpiresAt,
                    LastActivity = DateTime.UtcNow
                };

                // Update cache with new expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = newExpiresAt,
                    SlidingExpiration = TimeSpan.FromMinutes(_options.DeviceInactivityTimeoutMinutes),
                    Priority = CacheItemPriority.Normal
                };

                cache.Set(GetSessionCacheKey(sessionId), updatedSessionData, cacheOptions);

                logger.LogInformation("Session {SessionId} extended until {ExpiresAt}", sessionId, newExpiresAt);

                return updatedSessionData.ToSessionInfo();
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(Guid sessionId, SessionEndReason reason, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Closing session {SessionId}, Reason: {Reason}", sessionId, reason);

        await Task.Run(() =>
        {
            cache.Remove(GetSessionCacheKey(sessionId));
            _sessionLocks.TryRemove(sessionId, out _);
        }, cancellationToken);

        logger.LogInformation("Session {SessionId} closed successfully", sessionId);
    }

    /// <inheritdoc />
    public async Task<(int ExpiredSessions, int InactiveDevices)> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Starting cleanup of expired sessions and inactive devices");

        var expiredSessions = 0;
        var inactiveDevices = 0;

        await Task.Run(() =>
        {
            // Note: In a real implementation, you might need a more sophisticated way to track all sessions
            // since IMemoryCache doesn't provide enumeration. For this implementation, we rely on
            // the cache's automatic expiration and our periodic cleanup.
            
            // This is a simplified cleanup that would work better with a persistent store
            // For now, we'll just clean up our lock dictionary of removed sessions
            var keysToRemove = new List<Guid>();
            
            foreach (var kvp in _sessionLocks)
            {
                if (!cache.TryGetValue(GetSessionCacheKey(kvp.Key), out _))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _sessionLocks.TryRemove(key, out _);
                expiredSessions++;
            }

            // Reset hourly statistics if needed
            ResetHourlyStatsIfNeeded();

        }, cancellationToken);

        if (expiredSessions > 0 || inactiveDevices > 0)
        {
            logger.LogInformation("Cleanup completed: {ExpiredSessions} expired sessions, {InactiveDevices} inactive devices", 
                expiredSessions, inactiveDevices);
        }

        return (expiredSessions, inactiveDevices);
    }

    /// <inheritdoc />
    public async Task<SessionStatistics> GetSessionStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            lock (_statsLock)
            {
                // This is a simplified implementation since IMemoryCache doesn't support enumeration
                // In a production system, you'd likely use a persistent store with proper querying capabilities
                
                var activeSessions = _sessionLocks.Count;
                var totalDevices = 0;
                var oldestSessionAge = TimeSpan.Zero;

                // Approximate calculations based on available data
                var averageDevicesPerSession = activeSessions > 0 ? (double)totalDevices / activeSessions : 0;

                ResetHourlyStatsIfNeeded();

                return new SessionStatistics(
                    activeSessions,
                    totalDevices,
                    averageDevicesPerSession,
                    oldestSessionAge,
                    _sessionsCreatedInLastHour);
            }
        }, cancellationToken);
    }

    #region Private Methods

    /// <summary>
    /// Generates a cryptographically secure session ID.
    /// </summary>
    private Guid GenerateSecureSessionId()
    {
        // Use cryptographically secure random number generation
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);
        
        // Ensure version and variant bits are set correctly for UUID v4
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40); // Version 4
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // Variant bits
        
        return new Guid(bytes);
    }

    /// <summary>
    /// Gets session data from cache.
    /// </summary>
    private async Task<SessionData> GetSessionDataAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            if (!cache.TryGetValue(GetSessionCacheKey(sessionId), out SessionData sessionData))
            {
                throw new SessionNotFoundException(sessionId);
            }
            return sessionData;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the cache key for a session.
    /// </summary>
    private static string GetSessionCacheKey(Guid sessionId) => $"session_{sessionId}";

    /// <summary>
    /// Generates QR code URL for session joining.
    /// </summary>
    private static string GenerateQrCodeUrl(Guid sessionId)
    {
        // In a real application, this would be configurable based on environment
        return $"https://localhost:5001/join/{sessionId}";
    }

    /// <summary>
    /// Gets the count of active sessions.
    /// </summary>
    private async Task<int> GetActiveSessionCountAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() => _sessionLocks.Count, cancellationToken);
    }

    /// <summary>
    /// Updates session creation statistics.
    /// </summary>
    private void UpdateSessionCreationStats()
    {
        lock (_statsLock)
        {
            ResetHourlyStatsIfNeeded();
            Interlocked.Increment(ref _sessionsCreatedInLastHour);
        }
    }

    /// <summary>
    /// Resets hourly statistics if an hour has passed.
    /// </summary>
    private void ResetHourlyStatsIfNeeded()
    {
        if (DateTime.UtcNow - _lastHourReset >= TimeSpan.FromHours(1))
        {
            _sessionsCreatedInLastHour = 0;
            _lastHourReset = DateTime.UtcNow;
        }
    }

    #endregion
}
