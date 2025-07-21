using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Services;

/// <summary>
/// Service for generating QR codes for session joining functionality.
/// Implements caching, performance optimization, and comprehensive error handling.
/// </summary>
public sealed class QrCodeGenerator(
    IMemoryCache cache,
    IOptions<QrCodeGeneratorOptions> options,
    ILogger<QrCodeGenerator> logger) : IQrCodeGenerator
{
    private readonly QrCodeGeneratorOptions _options = options.Value;
    private readonly SemaphoreSlim _semaphore = new(options.Value.MaxConcurrentOperations);
    
    // Regex for extracting session ID from join URLs
    private static readonly Regex SessionIdRegex = new(@"/join/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public async Task<string> GenerateQrCodeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Generating QR code for session {SessionId}", sessionId);

        try
        {
            // Check cache first if caching is enabled
            if (_options.EnableCaching)
            {
                var cacheKey = GetCacheKey(sessionId);
                if (cache.TryGetValue(cacheKey, out string? cachedQrCode) && cachedQrCode != null)
                {
                    logger.LogDebug("QR code for session {SessionId} found in cache", sessionId);
                    return cachedQrCode;
                }
            }

            // Generate join URL
            var joinUrl = GenerateJoinUrl(sessionId);
            
            // Generate QR code with default settings
            var qrCodeBase64 = await GenerateQrCodeAsync(
                joinUrl, 
                _options.PixelsPerModule, 
                _options.DefaultErrorCorrectionLevel, 
                cancellationToken);

            // Cache the result if caching is enabled
            if (_options.EnableCaching)
            {
                var cacheKey = GetCacheKey(sessionId);
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };
                cache.Set(cacheKey, qrCodeBase64, cacheOptions);
            }

            logger.LogInformation("Successfully generated QR code for session {SessionId}", sessionId);
            return qrCodeBase64;
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions without wrapping
            throw;
        }
        catch (Exception ex) when (ex is not QrCodeGenerationException)
        {
            logger.LogError(ex, "Failed to generate QR code for session {SessionId}", sessionId);
            throw new QrCodeGenerationException(sessionId, "Failed to generate QR code", ex);
        }
    }

    /// <inheritdoc />
    public string GenerateJoinUrl(Guid sessionId, string? baseUrl = null)
    {
        try
        {
            var urlBase = baseUrl ?? _options.BaseUrl;
            var path = string.Format(_options.JoinUrlTemplate, sessionId);
            
            // Ensure base URL doesn't end with slash and path starts with slash
            urlBase = urlBase.TrimEnd('/');
            path = path.StartsWith('/') ? path : "/" + path;
            
            var joinUrl = urlBase + path;
            
            // Validate URL format
            if (!Uri.TryCreate(joinUrl, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Generated URL is invalid: {joinUrl}");
            }

            logger.LogDebug("Generated join URL for session {SessionId}: {JoinUrl}", sessionId, joinUrl);
            return joinUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate join URL for session {SessionId}", sessionId);
            throw new QrCodeGenerationException(sessionId, "Failed to generate join URL", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateQrCodeAsync(string qrCodeBase64, string expectedContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrCodeBase64))
        {
            logger.LogWarning("Cannot validate null or empty QR code");
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedContent))
        {
            logger.LogWarning("Cannot validate QR code against null or empty expected content");
            return false;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            try
            {
                using var timeoutCts = new CancellationTokenSource(_options.GenerationTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Decode base64 to bytes and validate the format
                var imageBytes = Convert.FromBase64String(qrCodeBase64);
                
                // Basic validation - check if we have valid image data
                // PNG files start with specific bytes: 89 50 4E 47 0D 0A 1A 0A
                var isValidPng = imageBytes.Length > 8 &&
                                imageBytes[0] == 0x89 &&
                                imageBytes[1] == 0x50 &&
                                imageBytes[2] == 0x4E &&
                                imageBytes[3] == 0x47 &&
                                imageBytes[4] == 0x0D &&
                                imageBytes[5] == 0x0A &&
                                imageBytes[6] == 0x1A &&
                                imageBytes[7] == 0x0A;
                
                logger.LogDebug("QR code validation completed successfully. Valid PNG: {IsValid}", isValidPng);
                return isValidPng;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QR code validation failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateQrCodeAsync(string content, int pixelsPerModule = 10, QrCodeErrorCorrection errorCorrectionLevel = QrCodeErrorCorrection.M, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty", nameof(content));
        }

        if (pixelsPerModule <= 0)
        {
            throw new ArgumentException("Pixels per module must be positive", nameof(pixelsPerModule));
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            try
            {
                using var timeoutCts = new CancellationTokenSource(_options.GenerationTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var stopwatch = Stopwatch.StartNew();

                // Convert our error correction level to QRCoder's enum
                var qrErrorLevel = errorCorrectionLevel switch
                {
                    QrCodeErrorCorrection.L => QRCodeGenerator.ECCLevel.L,
                    QrCodeErrorCorrection.M => QRCodeGenerator.ECCLevel.M,
                    QrCodeErrorCorrection.Q => QRCodeGenerator.ECCLevel.Q,
                    QrCodeErrorCorrection.H => QRCodeGenerator.ECCLevel.H,
                    _ => QRCodeGenerator.ECCLevel.M
                };

                // Generate QR code data
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(content, qrErrorLevel);
                
                // Create PNG QR code
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(pixelsPerModule, drawQuietZones: _options.IncludeBorder);
                
                // Convert to base64
                var base64String = Convert.ToBase64String(qrCodeBytes);
                
                stopwatch.Stop();
                logger.LogDebug("QR code generated successfully in {ElapsedMs}ms. Content length: {ContentLength}, Size: {PixelsPerModule}px/module, Error correction: {ErrorCorrection}",
                    stopwatch.ElapsedMilliseconds, content.Length, pixelsPerModule, errorCorrectionLevel);

                return base64String;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("QR code generation was cancelled or timed out");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate QR code for content: {Content}", content);
            throw new QrCodeGenerationException(Guid.Empty, "Failed to generate QR code", ex);
        }
    }

    /// <summary>
    /// Extracts session ID from a join URL if possible.
    /// </summary>
    /// <param name="url">URL to extract session ID from</param>
    /// <returns>Session ID if found, null otherwise</returns>
    public static Guid? ExtractSessionIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = SessionIdRegex.Match(url);
        if (match.Success && Guid.TryParse(match.Groups[1].Value, out var sessionId))
        {
            return sessionId;
        }

        return null;
    }

    /// <summary>
    /// Gets the cache key for a session's QR code.
    /// </summary>
    private static string GetCacheKey(Guid sessionId) => $"qrcode_{sessionId}";
}
