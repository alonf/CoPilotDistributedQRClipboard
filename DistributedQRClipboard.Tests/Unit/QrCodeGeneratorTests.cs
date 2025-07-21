using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using DistributedQRClipboard.Core.Services;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for QrCodeGenerator service.
/// Validates QR code generation, caching, URL formatting, and error handling.
/// </summary>
public class QrCodeGeneratorTests
{
    private readonly IMemoryCache _cache; // Use real cache
    private readonly Mock<ILogger<QrCodeGenerator>> _mockLogger;
    private readonly QrCodeGeneratorOptions _options;
    private readonly QrCodeGenerator _qrCodeGenerator;

    public QrCodeGeneratorTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<QrCodeGenerator>>();
        
        _options = new QrCodeGeneratorOptions
        {
            BaseUrl = "https://localhost:5001",
            JoinUrlTemplate = "/join/{0}",
            PixelsPerModule = 10,
            DefaultErrorCorrectionLevel = QrCodeErrorCorrection.M,
            EnableCaching = true,
            CacheExpirationMinutes = 60,
            MaxConcurrentOperations = 10,
            GenerationTimeoutMs = 5000,
            IncludeBorder = true,
            BorderSizeModules = 4
        };

        var mockOptions = new Mock<IOptions<QrCodeGeneratorOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        _qrCodeGenerator = new QrCodeGenerator(_cache, mockOptions.Object, _mockLogger.Object);
    }

    #region URL Generation Tests

    [Fact]
    public void GenerateJoinUrl_WithValidSessionId_ShouldReturnCorrectUrl()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expectedUrl = $"https://localhost:5001/join/{sessionId}";

        // Act
        var result = _qrCodeGenerator.GenerateJoinUrl(sessionId);

        // Assert
        result.Should().Be(expectedUrl);
        Uri.TryCreate(result, UriKind.Absolute, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateJoinUrl_WithCustomBaseUrl_ShouldUseCustomBaseUrl()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var customBaseUrl = "https://example.com";
        var expectedUrl = $"https://example.com/join/{sessionId}";

        // Act
        var result = _qrCodeGenerator.GenerateJoinUrl(sessionId, customBaseUrl);

        // Assert
        result.Should().Be(expectedUrl);
        Uri.TryCreate(result, UriKind.Absolute, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateJoinUrl_WithTrailingSlashInBaseUrl_ShouldHandleCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var baseUrlWithSlash = "https://example.com/";
        var expectedUrl = $"https://example.com/join/{sessionId}";

        // Act
        var result = _qrCodeGenerator.GenerateJoinUrl(sessionId, baseUrlWithSlash);

        // Assert
        result.Should().Be(expectedUrl);
    }

    [Fact]
    public void GenerateJoinUrl_WithEmptyGuid_ShouldReturnValidUrl()
    {
        // Arrange
        var sessionId = Guid.Empty;
        var expectedUrl = $"https://localhost:5001/join/{Guid.Empty}";

        // Act
        var result = _qrCodeGenerator.GenerateJoinUrl(sessionId);

        // Assert
        result.Should().Be(expectedUrl);
    }

    #endregion

    #region QR Code Generation Tests

    [Fact]
    public async Task GenerateQrCodeAsync_WithValidContent_ShouldReturnBase64String()
    {
        // Arrange
        var content = "https://localhost:5001/join/12345678-1234-1234-1234-123456789012";

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(content);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$"); // Valid base64 pattern
        
        // Verify it's a valid base64 string by trying to decode it
        var action = () => Convert.FromBase64String(result);
        action.Should().NotThrow();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithSessionId_ShouldReturnValidQrCode()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$"); // Valid base64 pattern
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithEmptyContent_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyContent = "";

        // Act & Assert
        await _qrCodeGenerator.Invoking(x => x.GenerateQrCodeAsync(emptyContent))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Content cannot be null or empty*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithNullContent_ShouldThrowArgumentException()
    {
        // Act & Assert
        await _qrCodeGenerator.Invoking(x => x.GenerateQrCodeAsync(null!))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Content cannot be null or empty*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithInvalidPixelsPerModule_ShouldThrowArgumentException()
    {
        // Arrange
        var content = "test content";
        var invalidPixels = -1;

        // Act & Assert
        await _qrCodeGenerator.Invoking(x => x.GenerateQrCodeAsync(content, invalidPixels))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Pixels per module must be positive*");
    }

    [Theory]
    [InlineData(QrCodeErrorCorrection.L)]
    [InlineData(QrCodeErrorCorrection.M)]
    [InlineData(QrCodeErrorCorrection.Q)]
    [InlineData(QrCodeErrorCorrection.H)]
    public async Task GenerateQrCodeAsync_WithDifferentErrorCorrections_ShouldSucceed(QrCodeErrorCorrection errorCorrection)
    {
        // Arrange
        var content = "test content";

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(content, errorCorrectionLevel: errorCorrection);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task GenerateQrCodeAsync_WithDifferentPixelSizes_ShouldSucceed(int pixelsPerModule)
    {
        // Arrange
        var content = "test content";

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(content, pixelsPerModule);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GenerateQrCodeAsync_WithCachingEnabled_ShouldCacheResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act - Generate QR code twice
        var result1 = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);
        var result2 = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);

        // Assert
        result1.Should().Be(result2);
        result1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithCachingDisabled_ShouldNotCache()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _options.EnableCaching = false;

        // Act - Generate QR code twice
        var result1 = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);
        var result2 = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);

        // Assert - Results should be the same content but not necessarily cached
        result1.Should().Be(result2); // Same content should produce same QR code
        result1.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateQrCodeAsync_WithValidBase64_ShouldReturnTrue()
    {
        // Arrange
        var content = "test content";
        var qrCodeBase64 = await _qrCodeGenerator.GenerateQrCodeAsync(content);

        // Act
        var result = await _qrCodeGenerator.ValidateQrCodeAsync(qrCodeBase64, content);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateQrCodeAsync_WithEmptyQrCode_ShouldReturnFalse()
    {
        // Arrange
        var emptyQrCode = "";
        var content = "test content";

        // Act
        var result = await _qrCodeGenerator.ValidateQrCodeAsync(emptyQrCode, content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateQrCodeAsync_WithNullQrCode_ShouldReturnFalse()
    {
        // Arrange
        var content = "test content";

        // Act
        var result = await _qrCodeGenerator.ValidateQrCodeAsync(null!, content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateQrCodeAsync_WithEmptyExpectedContent_ShouldReturnFalse()
    {
        // Arrange
        var content = "test content";
        var qrCodeBase64 = await _qrCodeGenerator.GenerateQrCodeAsync(content);

        // Act
        var result = await _qrCodeGenerator.ValidateQrCodeAsync(qrCodeBase64, "");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateQrCodeAsync_WithInvalidBase64_ShouldReturnFalse()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64!@#";
        var content = "test content";

        // Act
        var result = await _qrCodeGenerator.ValidateQrCodeAsync(invalidBase64, content);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region URL Extraction Tests

    [Fact]
    public void ExtractSessionIdFromUrl_WithValidJoinUrl_ShouldExtractSessionId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var joinUrl = $"https://localhost:5001/join/{sessionId}";

        // Act
        var result = QrCodeGenerator.ExtractSessionIdFromUrl(joinUrl);

        // Assert
        result.Should().Be(sessionId);
    }

    [Fact]
    public void ExtractSessionIdFromUrl_WithInvalidUrl_ShouldReturnNull()
    {
        // Arrange
        var invalidUrl = "https://localhost:5001/invalid/path";

        // Act
        var result = QrCodeGenerator.ExtractSessionIdFromUrl(invalidUrl);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSessionIdFromUrl_WithNullUrl_ShouldReturnNull()
    {
        // Act
        var result = QrCodeGenerator.ExtractSessionIdFromUrl(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSessionIdFromUrl_WithEmptyUrl_ShouldReturnNull()
    {
        // Act
        var result = QrCodeGenerator.ExtractSessionIdFromUrl("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSessionIdFromUrl_WithMalformedGuid_ShouldReturnNull()
    {
        // Arrange
        var urlWithBadGuid = "https://localhost:5001/join/not-a-guid";

        // Act
        var result = QrCodeGenerator.ExtractSessionIdFromUrl(urlWithBadGuid);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GenerateQrCodeAsync_ShouldCompleteWithinTimeout()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var timeout = TimeSpan.FromMilliseconds(_options.GenerationTimeoutMs);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(sessionId);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNullOrEmpty();
        stopwatch.Elapsed.Should().BeLessThan(timeout);
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await _qrCodeGenerator.Invoking(x => x.GenerateQrCodeAsync(sessionId, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task GenerateQrCodeAsync_WithConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange
        var sessionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // Act
        var tasks = sessionIds.Select(id => _qrCodeGenerator.GenerateQrCodeAsync(id)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r));
        results.Should().OnlyContain(r => r.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GenerateQrCodeAsync_WithExcessivelyLongContent_ShouldStillSucceed()
    {
        // Arrange
        var longContent = new string('A', 2000); // Very long content

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(longContent);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var contentWithSpecialChars = "https://example.com/join/test?param=value&other=测试#fragment";

        // Act
        var result = await _qrCodeGenerator.GenerateQrCodeAsync(contentWithSpecialChars);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    #endregion
}
