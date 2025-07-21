using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using DistributedQRClipboard.Core.Services;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for ClipboardManager service.
/// Validates clipboard content management with validation, storage, and real-time synchronization.
/// 
/// References:
/// - Requirements: User Story 3 (Copy Text to Shared Clipboard), User Story 4 (Paste Text from Shared Clipboard), User Story 5 (Real-time Synchronization)
/// - Design: SOLID Principles Implementation, Data Transfer Objects (Clipboard-Related DTOs)
/// </summary>
public class ClipboardManagerTests
{
    private readonly IMemoryCache _cache; // Use real cache instead of mock
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<IClipboardNotificationService> _mockNotificationService;
    private readonly Mock<ILogger<ClipboardManager>> _mockLogger;
    private readonly ClipboardManagerOptions _options;
    private readonly ClipboardManager _clipboardManager;

    public ClipboardManagerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions()); // Real cache
        _mockSessionManager = new Mock<ISessionManager>();
        _mockNotificationService = new Mock<IClipboardNotificationService>();
        _mockLogger = new Mock<ILogger<ClipboardManager>>();
        
        _options = new ClipboardManagerOptions
        {
            MaxContentLength = 10240,
            MaxContentSizeBytes = 10240,
            ClipboardCacheExpirationMinutes = 60,
            MaxConcurrentOperations = 5,
            MaxHistoryRequestLimit = 50,
            EnableContentSanitization = true,
            EnableSensitiveDataDetection = true
        };

        var mockOptions = new Mock<IOptions<ClipboardManagerOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        _clipboardManager = new ClipboardManager(
            _cache,
            _mockSessionManager.Object,
            _mockNotificationService.Object,
            mockOptions.Object,
            _mockLogger.Object);
    }

    #region Content Validation Tests

    [Fact]
    public async Task ValidateContentAsync_WithValidContent_ShouldReturnValid()
    {
        // Arrange
        var content = "Valid clipboard content";

        // Act
        var result = await _clipboardManager.ValidateContentAsync(content);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ContentLength.Should().Be(content.Length);
        result.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateContentAsync_WithEmptyContent_ShouldReturnInvalid()
    {
        // Arrange
        var content = "";

        // Act
        var result = await _clipboardManager.ValidateContentAsync(content);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateContentAsync_WithWhitespaceOnlyContent_ShouldReturnInvalid()
    {
        // Arrange
        var content = "   \t\n  ";

        // Act
        var result = await _clipboardManager.ValidateContentAsync(content);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("whitespace");
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateContentAsync_WithOversizedContent_ShouldReturnInvalid()
    {
        // Arrange
        var content = new string('a', _options.MaxContentLength + 1);

        // Act
        var result = await _clipboardManager.ValidateContentAsync(content);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum allowed");
        result.ContentLength.Should().Be(content.Length);
    }

    [Fact]
    public async Task ValidateContentAsync_WithSensitiveData_ShouldDetectAndFlag()
    {
        // Arrange
        var content = "My password is secret123";

        // Act
        var result = await _clipboardManager.ValidateContentAsync(content);

        // Assert
        result.IsValid.Should().BeTrue(); // Content is valid but flagged
        result.ContainsSensitiveData.Should().BeTrue();
    }

    #endregion

    #region Copy to Clipboard Tests

    [Fact]
    public async Task CopyToClipboardAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var content = "Test clipboard content";
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        
        var request = new CopyToClipboardRequest(content, sessionId, deviceId);

        // Setup mocks
        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });
        _mockSessionManager.Setup(x => x.UpdateDeviceActivityAsync(sessionId, deviceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService.Setup(x => x.NotifyClipboardUpdatedAsync(
            sessionId, deviceId, It.IsAny<ClipboardUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _clipboardManager.CopyToClipboardAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ClipboardContent.Should().NotBeNull();
        result.ClipboardContent!.Value.Content.Should().Be(content);
        result.ClipboardContent!.Value.DeviceId.Should().Be(deviceId);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CopyToClipboardAsync_WithInvalidSession_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var request = new CopyToClipboardRequest("content", sessionId, deviceId);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _clipboardManager.CopyToClipboardAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Session not found");
        result.ClipboardContent.Should().BeNull();
    }

    [Fact]
    public async Task CopyToClipboardAsync_WithInvalidContent_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        var request = new CopyToClipboardRequest("", sessionId, deviceId); // Empty content

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });

        // Act
        var result = await _clipboardManager.CopyToClipboardAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Content cannot be empty");
        result.ClipboardContent.Should().BeNull();
    }

    [Fact]
    public async Task CopyToClipboardAsync_WithUnauthorizedDevice_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var unauthorizedDeviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        
        var request = new CopyToClipboardRequest("content", sessionId, unauthorizedDeviceId);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device }); // Only authorized device

        // Act
        var result = await _clipboardManager.CopyToClipboardAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not authorized");
        result.ClipboardContent.Should().BeNull();
    }

    #endregion

    #region Get Clipboard Tests

    [Fact]
    public async Task GetClipboardAsync_WithValidRequest_ShouldReturnCurrentContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        var request = new GetClipboardRequest(sessionId, deviceId);

        var clipboardContent = ClipboardContent.Create("Test content", deviceId);
        var clipboardData = CreateClipboardData(sessionId, clipboardContent);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });
        _mockSessionManager.Setup(x => x.UpdateDeviceActivityAsync(sessionId, deviceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Populate cache with test data
        _cache.Set($"clipboard_{sessionId}", clipboardData);

        // Act
        var result = await _clipboardManager.GetClipboardAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ClipboardContent.Should().NotBeNull();
        result.ClipboardContent!.Value.Content.Should().Be("Test content");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetClipboardAsync_WithEmptyClipboard_ShouldReturnSuccessWithNullContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        var request = new GetClipboardRequest(sessionId, deviceId);

        var clipboardData = CreateClipboardData(sessionId, null);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });

        // Populate cache with test data (null content)
        _cache.Set($"clipboard_{sessionId}", clipboardData);

        // Act
        var result = await _clipboardManager.GetClipboardAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ClipboardContent.Should().BeNull();
        result.ErrorMessage.Should().Be("No content available");
    }

    #endregion

    #region Clear Clipboard Tests

    [Fact]
    public async Task ClearClipboardAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);

        var clipboardContent = ClipboardContent.Create("Test content", deviceId);
        var clipboardData = CreateClipboardData(sessionId, clipboardContent);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });
        _mockSessionManager.Setup(x => x.UpdateDeviceActivityAsync(sessionId, deviceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Populate cache with test data
        _cache.Set($"clipboard_{sessionId}", clipboardData);

        _mockNotificationService.Setup(x => x.NotifyClipboardClearedAsync(
            sessionId, deviceId, device, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _clipboardManager.ClearClipboardAsync(sessionId, deviceId);

        // Assert
        result.Should().BeTrue();
        _mockNotificationService.Verify(x => x.NotifyClipboardClearedAsync(
            sessionId, deviceId, device, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Clipboard History Tests

    [Fact]
    public async Task GetClipboardHistoryAsync_WithValidRequest_ShouldReturnHistory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var device = new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow);

        var content1 = ClipboardContent.Create("Content 1", deviceId);
        var content2 = ClipboardContent.Create("Content 2", deviceId);
        var history = new List<ClipboardHistoryEntry>
        {
            new(Guid.NewGuid(), content2, 0), // Most recent
            new(Guid.NewGuid(), content1, 1)  // Older
        };

        var clipboardData = CreateClipboardData(sessionId, content2, history);

        _mockSessionManager.Setup(x => x.ValidateSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSessionManager.Setup(x => x.GetSessionDevicesAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceInfo> { device });

        // Populate cache with test data
        _cache.Set($"clipboard_{sessionId}", clipboardData);

        // Act
        var result = await _clipboardManager.GetClipboardHistoryAsync(sessionId, deviceId, 10);

        // Assert
        result.Should().HaveCount(2);
        result[0].Content.Content.Should().Be("Content 2"); // Most recent first
        result[1].Content.Content.Should().Be("Content 1");
    }

    [Fact]
    public async Task GetClipboardHistoryAsync_WithLimitExceeded_ShouldThrowArgumentException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var invalidLimit = _options.MaxHistoryRequestLimit + 1;

        // Act & Assert
        await _clipboardManager.Invoking(x => x.GetClipboardHistoryAsync(sessionId, deviceId, invalidLimit))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("limit");
    }

    #endregion

    #region Clipboard Statistics Tests

    [Fact]
    public async Task GetClipboardStatisticsAsync_WithValidSession_ShouldReturnStatistics()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var statistics = new ClipboardStatistics(
            sessionId,
            5, // TotalCopyOperations
            1024, // TotalContentSize
            DateTime.UtcNow,
            new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow),
            new DeviceInfo(deviceId, "Test Device", DateTime.UtcNow, DateTime.UtcNow),
            204.8 // AverageContentSize
        );

        var clipboardData = CreateClipboardData(sessionId, null, new List<ClipboardHistoryEntry>(), statistics);

        _mockSessionManager.Setup(x => x.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionInfo(sessionId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 1, DateTime.UtcNow));

        // Populate cache with test data
        _cache.Set($"clipboard_{sessionId}", clipboardData);

        // Act
        var result = await _clipboardManager.GetClipboardStatisticsAsync(sessionId);

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.TotalCopyOperations.Should().Be(5);
        result.TotalContentSize.Should().Be(1024);
        result.AverageContentSize.Should().Be(204.8);
    }

    #endregion

    #region Helper Methods

    private static ClipboardData CreateClipboardData(
        Guid sessionId, 
        ClipboardContent? currentContent = null,
        IList<ClipboardHistoryEntry>? history = null,
        ClipboardStatistics? statistics = null)
    {
        var defaultStats = statistics ?? new ClipboardStatistics(
            sessionId,
            0,
            0,
            null,
            null,
            null,
            0.0);

        return new ClipboardData(
            sessionId,
            currentContent,
            history ?? new List<ClipboardHistoryEntry>(),
            defaultStats,
            DateTime.UtcNow,
            null);
    }

    #endregion
}
