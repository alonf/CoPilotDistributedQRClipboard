using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Core.Services;

/// <summary>
/// <summary>
/// Manages clipboard content with validation, storage, and real-time synchronization.
/// Implements secure content handling with comprehensive validation and monitoring.
/// </summary>
public sealed class ClipboardManager(
    IMemoryCache cache,
    ISessionManager sessionManager,
    IClipboardNotificationService notificationService,
    IOptions<ClipboardManagerOptions> options,
    ILogger<ClipboardManager> logger) : IClipboardManager
{
    private readonly ClipboardManagerOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionSemaphores = new();
    
    // Patterns for sensitive data detection
    private static readonly Regex CreditCardPattern = new(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex SsnPattern = new(@"\b\d{3}-?\d{2}-?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);
    
    /// <inheritdoc />
    public async Task<CopyToClipboardResponse> CopyToClipboardAsync(CopyToClipboardRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Copy request for session {SessionId} from device {DeviceId}", request.SessionId, request.DeviceId);

        try
        {
            // Validate session and device
            await ValidateSessionAndDeviceAsync(request.SessionId, request.DeviceId, cancellationToken);

            // Validate content
            var validationResult = await ValidateContentAsync(request.Content, cancellationToken);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Content validation failed for session {SessionId}: {Error}", request.SessionId, validationResult.ErrorMessage);
                return new CopyToClipboardResponse(null, false, validationResult.ErrorMessage);
            }

            // Get session semaphore for concurrency control
            var semaphore = _sessionSemaphores.GetOrAdd(request.SessionId, _ => new SemaphoreSlim(_options.MaxConcurrentOperations));
            
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Get device info for tracking
                var devices = await sessionManager.GetSessionDevicesAsync(request.SessionId, cancellationToken);
                var device = devices.FirstOrDefault(d => d.DeviceId == request.DeviceId);
                if (device.DeviceId == Guid.Empty) // Default value means not found
                {
                    throw new DeviceOperationException(request.DeviceId, "Device not found in session");
                }

                // Create clipboard content using the static factory method
                var content = ClipboardContent.Create(request.Content, request.DeviceId);

                // Update clipboard data
                var clipboardData = await GetOrCreateClipboardDataAsync(request.SessionId, cancellationToken);
                var updatedData = clipboardData.WithNewContent(content, device);
                
                // Store in cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.ClipboardCacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };
                
                cache.Set(GetClipboardCacheKey(request.SessionId), updatedData, cacheOptions);

                // Update device activity
                await sessionManager.UpdateDeviceActivityAsync(request.SessionId, request.DeviceId, cancellationToken);

                // Send real-time notifications
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var clipboardEvent = new ClipboardUpdatedEvent(
                            Guid.NewGuid(),
                            DateTime.UtcNow,
                            request.SessionId,
                            Guid.NewGuid().ToString(),
                            content,
                            request.DeviceId,
                            null);

                        var notifiedCount = await notificationService.NotifyClipboardUpdatedAsync(
                            request.SessionId, 
                            request.DeviceId, 
                            clipboardEvent, 
                            CancellationToken.None);
                        
                        logger.LogInformation("Notified {Count} devices about clipboard update in session {SessionId}", 
                            notifiedCount, request.SessionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to send clipboard update notifications for session {SessionId}", request.SessionId);
                    }
                }, CancellationToken.None);

                logger.LogInformation("Successfully copied content to clipboard for session {SessionId}. Content length: {Length}", 
                    request.SessionId, content.Content.Length);

                return new CopyToClipboardResponse(content, true);
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (SessionNotFoundException ex)
        {
            logger.LogWarning("Session not found: {SessionId}", ex.SessionId);
            return new CopyToClipboardResponse(null, false, "Session not found or has expired");
        }
        catch (InvalidSessionException ex)
        {
            logger.LogWarning("Invalid session {SessionId}: {Reason}", ex.SessionId, ex.Reason);
            return new CopyToClipboardResponse(null, false, "Session is not available");
        }
        catch (DeviceOperationException ex)
        {
            logger.LogWarning("Device operation failed for {DeviceId}: {Message}", ex.DeviceId, ex.Message);
            return new CopyToClipboardResponse(null, false, "Device is not authorized for this session");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error copying to clipboard for session {SessionId}", request.SessionId);
            return new CopyToClipboardResponse(null, false, "An unexpected error occurred while copying to clipboard");
        }
    }

    /// <inheritdoc />
    public async Task<GetClipboardResponse> GetClipboardAsync(GetClipboardRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Get clipboard request for session {SessionId} from device {DeviceId}", request.SessionId, request.DeviceId);

        try
        {
            // Validate session and device
            await ValidateSessionAndDeviceAsync(request.SessionId, request.DeviceId, cancellationToken);

            // Get clipboard data
            var clipboardData = await GetClipboardDataAsync(request.SessionId, cancellationToken);
            
            // Update device activity
            await sessionManager.UpdateDeviceActivityAsync(request.SessionId, request.DeviceId, cancellationToken);

            var response = clipboardData.CurrentContent != null
                ? new GetClipboardResponse(clipboardData.CurrentContent, true)
                : new GetClipboardResponse(null, true, "No content available");

            logger.LogDebug("Retrieved clipboard content for session {SessionId}. Has content: {HasContent}", 
                request.SessionId, clipboardData.CurrentContent != null);

            return response;
        }
        catch (SessionNotFoundException ex)
        {
            logger.LogWarning("Session not found: {SessionId}", ex.SessionId);
            return new GetClipboardResponse(null, false, "Session not found or has expired");
        }
        catch (InvalidSessionException ex)
        {
            logger.LogWarning("Invalid session {SessionId}: {Reason}", ex.SessionId, ex.Reason);
            return new GetClipboardResponse(null, false, "Session is not available");
        }
        catch (DeviceOperationException ex)
        {
            logger.LogWarning("Device operation failed for {DeviceId}: {Message}", ex.DeviceId, ex.Message);
            return new GetClipboardResponse(null, false, "Device is not authorized for this session");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error getting clipboard for session {SessionId}", request.SessionId);
            return new GetClipboardResponse(null, false, "An unexpected error occurred while retrieving clipboard content");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClipboardHistoryEntry>> GetClipboardHistoryAsync(Guid sessionId, Guid deviceId, int limit = 10, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Get clipboard history for session {SessionId} from device {DeviceId}, limit: {Limit}", sessionId, deviceId, limit);

        // Validate parameters
        if (limit <= 0 || limit > _options.MaxHistoryRequestLimit)
        {
            throw new ArgumentException($"Limit must be between 1 and {_options.MaxHistoryRequestLimit}", nameof(limit));
        }

        // Validate session and device
        await ValidateSessionAndDeviceAsync(sessionId, deviceId, cancellationToken);

        // Get clipboard data
        var clipboardData = await GetClipboardDataAsync(sessionId, cancellationToken);
        
        // Update device activity
        await sessionManager.UpdateDeviceActivityAsync(sessionId, deviceId, cancellationToken);

        // Return most recent entries
        var history = clipboardData.History
            .OrderByDescending(h => h.Content.CreatedAt)
            .Take(limit)
            .ToList()
            .AsReadOnly();

        logger.LogDebug("Retrieved {Count} history entries for session {SessionId}", history.Count, sessionId);

        return history;
    }

    /// <inheritdoc />
    public async Task<bool> ClearClipboardAsync(Guid sessionId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Clear clipboard request for session {SessionId} from device {DeviceId}", sessionId, deviceId);

        try
        {
            // Validate session and device
            await ValidateSessionAndDeviceAsync(sessionId, deviceId, cancellationToken);

            // Get session semaphore for concurrency control
            var semaphore = _sessionSemaphores.GetOrAdd(sessionId, _ => new SemaphoreSlim(_options.MaxConcurrentOperations));
            
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Get device info for tracking
                var devices = await sessionManager.GetSessionDevicesAsync(sessionId, cancellationToken);
                var device = devices.FirstOrDefault(d => d.DeviceId == deviceId);
                if (device.DeviceId == Guid.Empty) // Default value means not found
                {
                    throw new DeviceOperationException(deviceId, "Device not found in session");
                }

                // Get and update clipboard data
                var clipboardData = await GetClipboardDataAsync(sessionId, cancellationToken);
                var updatedData = clipboardData.WithClearedContent(device);
                
                // Store in cache
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.ClipboardCacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };
                
                cache.Set(GetClipboardCacheKey(sessionId), updatedData, cacheOptions);

                // Update device activity
                await sessionManager.UpdateDeviceActivityAsync(sessionId, deviceId, cancellationToken);

                // Send real-time notifications
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notifiedCount = await notificationService.NotifyClipboardClearedAsync(
                            sessionId, 
                            deviceId, 
                            device, 
                            CancellationToken.None);
                        
                        logger.LogInformation("Notified {Count} devices about clipboard clear in session {SessionId}", 
                            notifiedCount, sessionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to send clipboard clear notifications for session {SessionId}", sessionId);
                    }
                }, CancellationToken.None);

                logger.LogInformation("Successfully cleared clipboard for session {SessionId}", sessionId);
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear clipboard for session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public Task<ContentValidationResult> ValidateContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var result = ValidateContent(content);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<ClipboardStatistics> GetClipboardStatisticsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Get clipboard statistics for session {SessionId}", sessionId);

        // Validate session exists
        await sessionManager.GetSessionAsync(sessionId, cancellationToken);

        // Get clipboard data
        var clipboardData = await GetClipboardDataAsync(sessionId, cancellationToken);

        return clipboardData.Statistics;
    }

    #region Private Methods

    /// <summary>
    /// Validates that a session exists and device is authorized.
    /// </summary>
    private async Task ValidateSessionAndDeviceAsync(Guid sessionId, Guid deviceId, CancellationToken cancellationToken)
    {
        // Validate session exists and is active
        var isValid = await sessionManager.ValidateSessionAsync(sessionId, cancellationToken);
        if (!isValid)
        {
            throw new SessionNotFoundException(sessionId);
        }

        // Validate device is part of session
        var devices = await sessionManager.GetSessionDevicesAsync(sessionId, cancellationToken);
        if (!devices.Any(d => d.DeviceId == deviceId))
        {
            throw new DeviceOperationException(deviceId, "Device is not part of this session");
        }
    }

    /// <summary>
    /// Gets clipboard data from cache, creating if it doesn't exist.
    /// </summary>
    private Task<ClipboardData> GetOrCreateClipboardDataAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var cacheKey = GetClipboardCacheKey(sessionId);
        
        if (cache.TryGetValue(cacheKey, out ClipboardData? existingData) && existingData != null)
        {
            return Task.FromResult(existingData);
        }

        // Create new clipboard data
        var initialStats = new ClipboardStatistics(
            sessionId,
            0, // TotalCopyOperations
            0, // TotalContentSize
            null, // LastUpdatedAt
            null, // LastUpdatedBy
            null, // MostActiveDevice
            0.0); // AverageContentSize

        var newData = new ClipboardData(
            sessionId,
            null, // CurrentContent
            new List<ClipboardHistoryEntry>(),
            initialStats,
            DateTime.UtcNow,
            null);

        // Store in cache
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.ClipboardCacheExpirationMinutes),
            Priority = CacheItemPriority.Normal
        };
        
        cache.Set(cacheKey, newData, cacheOptions);

        return Task.FromResult(newData);
    }

    /// <summary>
    /// Gets clipboard data from cache.
    /// </summary>
    private async Task<ClipboardData> GetClipboardDataAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await GetOrCreateClipboardDataAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// Generates cache key for clipboard data.
    /// </summary>
    private static string GetClipboardCacheKey(Guid sessionId) => $"clipboard_{sessionId}";

    /// <summary>
    /// Validates content synchronously.
    /// </summary>
    private ContentValidationResult ValidateContent(string content)
    {
        // Check for null or empty content
        if (string.IsNullOrEmpty(content))
        {
            return new ContentValidationResult(false, "Content cannot be empty", 0, 0, true, false);
        }

        // Check for whitespace-only content
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ContentValidationResult(false, "Content cannot be empty or whitespace-only", content.Length, 0, true, false);
        }

        // Check content length
        if (content.Length > _options.MaxContentLength)
        {
            return new ContentValidationResult(
                false, 
                $"Content length ({content.Length:N0} characters) exceeds maximum allowed ({_options.MaxContentLength:N0} characters)",
                content.Length,
                0,
                false,
                false);
        }

        // Check content size in bytes
        var contentSizeBytes = Encoding.UTF8.GetByteCount(content);
        if (contentSizeBytes > _options.MaxContentSizeBytes)
        {
            return new ContentValidationResult(
                false,
                $"Content size ({contentSizeBytes:N0} bytes) exceeds maximum allowed ({_options.MaxContentSizeBytes:N0} bytes)",
                content.Length,
                contentSizeBytes,
                false,
                false);
        }

        // Sanitize content if enabled
        var sanitizedContent = _options.EnableContentSanitization ? SanitizeContent(content) : content;

        // Check for sensitive data if enabled
        var containsSensitiveData = _options.EnableSensitiveDataDetection && DetectSensitiveData(content);

        if (containsSensitiveData)
        {
            logger.LogWarning("Potentially sensitive data detected in clipboard content");
        }

        return new ContentValidationResult(
            true,
            null,
            sanitizedContent.Length,
            Encoding.UTF8.GetByteCount(sanitizedContent),
            false,
            containsSensitiveData);
    }

    /// <summary>
    /// Sanitizes content by removing potentially harmful characters.
    /// </summary>
    private static string SanitizeContent(string content)
    {
        // Remove control characters except common whitespace
        var sanitized = new StringBuilder(content.Length);
        
        foreach (char c in content)
        {
            if (char.IsControl(c))
            {
                // Allow common whitespace characters
                if (c == '\t' || c == '\n' || c == '\r')
                {
                    sanitized.Append(c);
                }
                // Skip other control characters
            }
            else
            {
                sanitized.Append(c);
            }
        }
        
        return sanitized.ToString();
    }

    /// <summary>
    /// Detects potentially sensitive data in content asynchronously.
    /// </summary>
    private async Task<bool> DetectSensitiveDataAsync(string content, CancellationToken cancellationToken = default)
    {
        // For now, delegate to synchronous version
        // In future, this could include async operations like ML-based detection
        return await Task.FromResult(DetectSensitiveData(content));
    }

    /// <summary>
    /// Detects potentially sensitive data in content.
    /// </summary>
    private static bool DetectSensitiveData(string content)
    {
        // Check for common sensitive data patterns
        return CreditCardPattern.IsMatch(content) ||
               SsnPattern.IsMatch(content) ||
               EmailPattern.IsMatch(content) ||
               content.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
