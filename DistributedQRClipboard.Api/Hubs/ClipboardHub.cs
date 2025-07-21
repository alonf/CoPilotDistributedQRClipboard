using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Api.Hubs;

/// <summary>
/// SignalR hub for real-time clipboard synchronization and device management.
/// </summary>
public sealed class ClipboardHub(
    ISessionManager sessionManager,
    IClipboardManager clipboardManager,
    ILogger<ClipboardHub> logger) : Hub
{
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly IClipboardManager _clipboardManager = clipboardManager;
    private readonly ILogger<ClipboardHub> _logger = logger;

    /// <summary>
    /// Handles client connection to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var clientIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("Client connected: {ConnectionId} from {ClientIp}", connectionId, clientIp);
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handles client disconnection from the hub.
    /// </summary>
    /// <param name="exception">Exception that caused disconnection, if any</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", connectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        }

        // Remove from all groups the connection might have joined
        await LeaveAllSessionsAsync();
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins a client to a session group for real-time updates.
    /// </summary>
    /// <param name="sessionId">The session ID to join</param>
    /// <param name="deviceId">The device ID of the client</param>
    /// <param name="deviceName">Optional device name</param>
    /// <returns>Join result</returns>
    public async Task<JoinSessionResult> JoinSessionAsync(string sessionId, string deviceId, string? deviceName = null)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            
            _logger.LogInformation("Device {DeviceId} attempting to join session {SessionId} via SignalR", deviceId, sessionId);

            // Validate session ID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                _logger.LogWarning("Invalid session ID format: {SessionId}", sessionId);
                return new JoinSessionResult(false, "Invalid session ID format");
            }

            // Validate device ID
            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                _logger.LogWarning("Invalid device ID format: {DeviceId}", deviceId);
                return new JoinSessionResult(false, "Invalid device ID format");
            }

            // Verify session exists
            var sessionInfo = await _sessionManager.GetSessionAsync(sessionGuid);
            
            // Join session through session manager
            var joinRequest = new JoinSessionRequest(sessionGuid, deviceGuid, deviceName);
            var joinResult = await _sessionManager.JoinSessionAsync(joinRequest);

            if (!joinResult.Success)
            {
                _logger.LogWarning("Failed to join session {SessionId}: {ErrorMessage}", sessionId, joinResult.ErrorMessage);
                return new JoinSessionResult(false, joinResult.ErrorMessage ?? "Failed to join session");
            }

            // Add connection to SignalR group
            var groupName = GetSessionGroupName(sessionGuid);
            await Groups.AddToGroupAsync(connectionId, groupName);

            // Store connection metadata
            Context.Items["SessionId"] = sessionGuid;
            Context.Items["DeviceId"] = deviceGuid;
            Context.Items["DeviceName"] = deviceName;

            _logger.LogInformation("Device {DeviceId} successfully joined session {SessionId} via SignalR", deviceId, sessionId);

            // Get current device count for the event
            var currentSession = await _sessionManager.GetSessionAsync(sessionGuid);
            var deviceCount = currentSession.DeviceCount;

            // Notify other devices in the session about new device
            var deviceJoinedEvent = DeviceJoinedEvent.Create(
                sessionGuid,
                new DeviceInfo(deviceGuid, deviceName, DateTime.UtcNow, DateTime.UtcNow),
                deviceCount);

            await Clients.GroupExcept(groupName, connectionId)
                .SendAsync("DeviceJoined", deviceJoinedEvent);

            return new JoinSessionResult(true, "Successfully joined session", joinResult.SessionInfo);
        }
        catch (SessionNotFoundException)
        {
            _logger.LogWarning("Session not found: {SessionId}", sessionId);
            return new JoinSessionResult(false, "Session not found or expired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining session {SessionId} for device {DeviceId}", sessionId, deviceId);
            return new JoinSessionResult(false, "An error occurred while joining the session");
        }
    }

    /// <summary>
    /// Removes a client from a session group.
    /// </summary>
    /// <param name="sessionId">The session ID to leave</param>
    /// <param name="deviceId">The device ID of the client</param>
    /// <returns>Leave result</returns>
    public async Task<LeaveSessionResult> LeaveSessionAsync(string sessionId, string deviceId)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            
            _logger.LogInformation("Device {DeviceId} leaving session {SessionId} via SignalR", deviceId, sessionId);

            // Validate session ID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new LeaveSessionResult(false, "Invalid session ID format");
            }

            // Validate device ID
            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return new LeaveSessionResult(false, "Invalid device ID format");
            }

            // Remove from SignalR group
            var groupName = GetSessionGroupName(sessionGuid);
            await Groups.RemoveFromGroupAsync(connectionId, groupName);

            // Leave session through session manager
            await _sessionManager.LeaveSessionAsync(sessionGuid, deviceGuid, DeviceLeaveReason.Disconnect);

            // Clear connection metadata
            Context.Items.Remove("SessionId");
            Context.Items.Remove("DeviceId");
            Context.Items.Remove("DeviceName");

            _logger.LogInformation("Device {DeviceId} successfully left session {SessionId} via SignalR", deviceId, sessionId);

            // Get device name and current session info for the event
            var deviceName = Context.Items["DeviceName"]?.ToString();
            var currentSession = await _sessionManager.GetSessionAsync(sessionGuid);
            var deviceCount = currentSession.DeviceCount;

            // Notify other devices in the session about device leaving
            var deviceLeftEvent = DeviceLeftEvent.Create(
                sessionGuid,
                deviceGuid,
                deviceName,
                deviceCount,
                DeviceLeaveReason.Disconnect);

            await Clients.GroupExcept(groupName, connectionId)
                .SendAsync("DeviceLeft", deviceLeftEvent);

            return new LeaveSessionResult(true, "Successfully left session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving session {SessionId} for device {DeviceId}", sessionId, deviceId);
            return new LeaveSessionResult(false, "An error occurred while leaving the session");
        }
    }

    /// <summary>
    /// Broadcasts clipboard update to all devices in a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="content">The clipboard content</param>
    /// <param name="deviceId">The device ID that initiated the update</param>
    /// <returns>Broadcast result</returns>
    public async Task<ClipboardUpdateResult> BroadcastClipboardUpdateAsync(string sessionId, string content, string deviceId)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            
            _logger.LogInformation("Broadcasting clipboard update for session {SessionId} from device {DeviceId}", sessionId, deviceId);

            // Validate session ID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new ClipboardUpdateResult(false, "Invalid session ID format");
            }

            // Validate device ID
            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return new ClipboardUpdateResult(false, "Invalid device ID format");
            }

            // Copy to clipboard through clipboard manager
            var copyRequest = new CopyToClipboardRequest(content, sessionGuid, deviceGuid);
            var copyResult = await _clipboardManager.CopyToClipboardAsync(copyRequest);

            if (!copyResult.Success)
            {
                _logger.LogWarning("Failed to copy to clipboard for session {SessionId}: {ErrorMessage}", sessionId, copyResult.ErrorMessage);
                return new ClipboardUpdateResult(false, copyResult.ErrorMessage ?? "Failed to copy to clipboard");
            }

            // Create clipboard update event
            var clipboardEvent = ClipboardUpdatedEvent.Create(
                sessionGuid,
                copyResult.ClipboardContent!.Value,
                deviceGuid);

            // Broadcast to all devices in session except sender
            var groupName = GetSessionGroupName(sessionGuid);
            await Clients.GroupExcept(groupName, connectionId)
                .SendAsync("ClipboardUpdated", clipboardEvent);

            _logger.LogInformation("Successfully broadcasted clipboard update for session {SessionId}", sessionId);

            return new ClipboardUpdateResult(true, "Clipboard updated successfully", copyResult.ClipboardContent);
        }
        catch (ClipboardValidationException ex)
        {
            _logger.LogWarning(ex, "Clipboard validation failed for session {SessionId}", sessionId);
            return new ClipboardUpdateResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting clipboard update for session {SessionId}", sessionId);
            return new ClipboardUpdateResult(false, "An error occurred while updating the clipboard");
        }
    }

    /// <summary>
    /// Broadcasts clipboard clear to all devices in a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID that initiated the clear</param>
    /// <returns>Clear result</returns>
    public async Task<ClipboardClearResult> BroadcastClipboardClearAsync(string sessionId, string deviceId)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            
            _logger.LogInformation("Broadcasting clipboard clear for session {SessionId} from device {DeviceId}", sessionId, deviceId);

            // Validate session ID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new ClipboardClearResult(false, "Invalid session ID format");
            }

            // Validate device ID
            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return new ClipboardClearResult(false, "Invalid device ID format");
            }

            // Clear clipboard through clipboard manager
            var clearResult = await _clipboardManager.ClearClipboardAsync(sessionGuid, deviceGuid);

            if (!clearResult)
            {
                _logger.LogWarning("Failed to clear clipboard for session {SessionId}", sessionId);
                return new ClipboardClearResult(false, "Failed to clear clipboard");
            }

            // Broadcast to all devices in session except sender
            var groupName = GetSessionGroupName(sessionGuid);
            var deviceName = Context.Items["DeviceName"]?.ToString();
            var clearedBy = new DeviceInfo(deviceGuid, deviceName, DateTime.UtcNow, DateTime.UtcNow);
            
            await Clients.GroupExcept(groupName, connectionId)
                .SendAsync("ClipboardCleared", new { SessionId = sessionGuid, ClearedBy = clearedBy, Timestamp = DateTime.UtcNow });

            _logger.LogInformation("Successfully broadcasted clipboard clear for session {SessionId}", sessionId);

            return new ClipboardClearResult(true, "Clipboard cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting clipboard clear for session {SessionId}", sessionId);
            return new ClipboardClearResult(false, "An error occurred while clearing the clipboard");
        }
    }

    /// <summary>
    /// Gets the current clipboard content for a session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="deviceId">The device ID requesting the content</param>
    /// <returns>The current clipboard content</returns>
    public async Task<ClipboardContentResult> GetClipboardContentAsync(string sessionId, string deviceId)
    {
        try
        {
            _logger.LogDebug("Getting clipboard content for session {SessionId} from device {DeviceId}", sessionId, deviceId);

            // Validate session ID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                return new ClipboardContentResult(false, "Invalid session ID format");
            }

            // Validate device ID
            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return new ClipboardContentResult(false, "Invalid device ID format");
            }

            // Get clipboard content through clipboard manager
            var getRequest = new GetClipboardRequest(sessionGuid, deviceGuid);
            var getResult = await _clipboardManager.GetClipboardAsync(getRequest);

            return new ClipboardContentResult(getResult.Success, getResult.ErrorMessage, getResult.ClipboardContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clipboard content for session {SessionId}", sessionId);
            return new ClipboardContentResult(false, "An error occurred while retrieving clipboard content");
        }
    }

    /// <summary>
    /// Removes the connection from all session groups.
    /// </summary>
    private async Task LeaveAllSessionsAsync()
    {
        if (Context.Items.TryGetValue("SessionId", out var sessionIdObj) && sessionIdObj is Guid sessionId)
        {
            if (Context.Items.TryGetValue("DeviceId", out var deviceIdObj) && deviceIdObj is Guid deviceId)
            {
                try
                {
                    // Leave session through session manager
                    await _sessionManager.LeaveSessionAsync(sessionId, deviceId, DeviceLeaveReason.Disconnect);
                    
                    // Remove from SignalR group
                    var groupName = GetSessionGroupName(sessionId);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                    
                    _logger.LogInformation("Connection {ConnectionId} removed from session {SessionId} on disconnect", 
                        Context.ConnectionId, sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing connection {ConnectionId} from session {SessionId}", 
                        Context.ConnectionId, sessionId);
                }
            }
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

/// <summary>
/// Result of joining a session via SignalR.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="Message">Result message</param>
/// <param name="SessionInfo">Session information if successful</param>
public sealed record JoinSessionResult(bool Success, string Message, SessionInfo? SessionInfo = null);

/// <summary>
/// Result of leaving a session via SignalR.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="Message">Result message</param>
public sealed record LeaveSessionResult(bool Success, string Message);

/// <summary>
/// Result of clipboard update operation.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="Message">Result message</param>
/// <param name="ClipboardContent">The clipboard content if successful</param>
public sealed record ClipboardUpdateResult(bool Success, string Message, ClipboardContent? ClipboardContent = null);

/// <summary>
/// Result of clipboard clear operation.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="Message">Result message</param>
public sealed record ClipboardClearResult(bool Success, string Message);

/// <summary>
/// Result of getting clipboard content.
/// </summary>
/// <param name="Success">Whether the operation was successful</param>
/// <param name="Message">Result message</param>
/// <param name="ClipboardContent">The clipboard content if successful</param>
public sealed record ClipboardContentResult(bool Success, string? Message, ClipboardContent? ClipboardContent = null);
