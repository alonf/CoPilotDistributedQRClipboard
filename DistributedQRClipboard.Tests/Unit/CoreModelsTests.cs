using FluentAssertions;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for core domain models and DTOs.
/// </summary>
public class CoreModelsTests
{
    [Fact]
    public void SessionInfo_ShouldBeImmutable()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(1);
        var lastActivity = createdAt.AddMinutes(30);

        // Act
        var sessionInfo = new SessionInfo(sessionId, createdAt, expiresAt, 2, lastActivity);

        // Assert
        sessionInfo.SessionId.Should().Be(sessionId);
        sessionInfo.CreatedAt.Should().Be(createdAt);
        sessionInfo.ExpiresAt.Should().Be(expiresAt);
        sessionInfo.DeviceCount.Should().Be(2);
        sessionInfo.LastActivity.Should().Be(lastActivity);
    }

    [Fact]
    public void SessionInfo_IsActive_ShouldReturnTrueForValidSession()
    {
        // Arrange
        var sessionInfo = new SessionInfo(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-30),
            DateTime.UtcNow.AddMinutes(30), // Expires in 30 minutes
            1,
            DateTime.UtcNow);

        // Act & Assert
        sessionInfo.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SessionInfo_IsActive_ShouldReturnFalseForExpiredSession()
    {
        // Arrange
        var sessionInfo = new SessionInfo(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddMinutes(-30), // Expired 30 minutes ago
            1,
            DateTime.UtcNow.AddMinutes(-30));

        // Act & Assert
        sessionInfo.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SessionInfo_RemainingTime_ShouldCalculateCorrectly()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var sessionInfo = new SessionInfo(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-30),
            expiresAt,
            1,
            DateTime.UtcNow);

        // Act
        var remainingTime = sessionInfo.RemainingTime;

        // Assert
        remainingTime.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateSessionRequest_IsValid_ShouldValidateExpirationMinutes()
    {
        // Arrange & Act & Assert
        new CreateSessionRequest(ExpirationMinutes: 60).IsValid.Should().BeTrue();
        new CreateSessionRequest(ExpirationMinutes: 1).IsValid.Should().BeTrue();
        new CreateSessionRequest(ExpirationMinutes: 1440).IsValid.Should().BeTrue();
        new CreateSessionRequest(ExpirationMinutes: 0).IsValid.Should().BeFalse();
        new CreateSessionRequest(ExpirationMinutes: -1).IsValid.Should().BeFalse();
        new CreateSessionRequest(ExpirationMinutes: 1441).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ClipboardContent_Create_ShouldGenerateValidHash()
    {
        // Arrange
        var content = "Test clipboard content";
        var deviceId = Guid.NewGuid();

        // Act
        var clipboardContent = ClipboardContent.Create(content, deviceId);

        // Assert
        clipboardContent.Content.Should().Be(content);
        clipboardContent.DeviceId.Should().Be(deviceId);
        clipboardContent.ContentHash.Should().NotBeNullOrEmpty();
        clipboardContent.IsHashValid.Should().BeTrue();
        clipboardContent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClipboardContent_SizeInBytes_ShouldCalculateCorrectly()
    {
        // Arrange
        var content = "Hello, 世界!"; // Mixed ASCII and Unicode
        var clipboardContent = ClipboardContent.Create(content, Guid.NewGuid());

        // Act
        var sizeInBytes = clipboardContent.SizeInBytes;

        // Assert
        var expectedSize = System.Text.Encoding.UTF8.GetByteCount(content);
        sizeInBytes.Should().Be(expectedSize);
    }

    [Fact]
    public void ClipboardContent_IsEmpty_ShouldDetectEmptyContent()
    {
        // Arrange & Act & Assert
        ClipboardContent.Create("", Guid.NewGuid()).IsEmpty.Should().BeTrue();
        ClipboardContent.Create("   ", Guid.NewGuid()).IsEmpty.Should().BeTrue();
        ClipboardContent.Create("Hello", Guid.NewGuid()).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ClipboardContent_Preview_ShouldTruncateLongContent()
    {
        // Arrange
        var longContent = new string('A', 150);
        var clipboardContent = ClipboardContent.Create(longContent, Guid.NewGuid());

        // Act
        var preview = clipboardContent.Preview;

        // Assert
        preview.Should().HaveLength(103); // 100 chars + "..."
        preview.Should().EndWith("...");
        preview.Should().StartWith("AAA");
    }

    [Fact]
    public void ClipboardContent_Preview_ShouldNotTruncateShortContent()
    {
        // Arrange
        var shortContent = "Short content";
        var clipboardContent = ClipboardContent.Create(shortContent, Guid.NewGuid());

        // Act
        var preview = clipboardContent.Preview;

        // Assert
        preview.Should().Be(shortContent);
        preview.Should().NotContain("...");
    }

    [Fact]
    public void CopyToClipboardRequest_IsValid_ShouldValidateAllFields()
    {
        // Arrange
        var validRequest = new CopyToClipboardRequest(
            "Valid content",
            Guid.NewGuid(),
            Guid.NewGuid());

        var invalidContentRequest = new CopyToClipboardRequest(
            "",
            Guid.NewGuid(),
            Guid.NewGuid());

        var invalidSessionRequest = new CopyToClipboardRequest(
            "Valid content",
            Guid.Empty,
            Guid.NewGuid());

        var invalidDeviceRequest = new CopyToClipboardRequest(
            "Valid content",
            Guid.NewGuid(),
            Guid.Empty);

        var tooLongContentRequest = new CopyToClipboardRequest(
            new string('A', 10241),
            Guid.NewGuid(),
            Guid.NewGuid());

        // Act & Assert
        validRequest.IsValid.Should().BeTrue();
        invalidContentRequest.IsValid.Should().BeFalse();
        invalidSessionRequest.IsValid.Should().BeFalse();
        invalidDeviceRequest.IsValid.Should().BeFalse();
        tooLongContentRequest.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeviceInfo_IsActive_ShouldDetectActiveDevices()
    {
        // Arrange
        var activeDevice = new DeviceInfo(
            Guid.NewGuid(),
            "Active Device",
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-2)); // Last seen 2 minutes ago

        var inactiveDevice = new DeviceInfo(
            Guid.NewGuid(),
            "Inactive Device",
            DateTime.UtcNow.AddMinutes(-20),
            DateTime.UtcNow.AddMinutes(-10)); // Last seen 10 minutes ago

        // Act & Assert
        activeDevice.IsActive.Should().BeTrue();
        inactiveDevice.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Records_ShouldImplementEqualityCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(1);
        var lastActivity = createdAt.AddMinutes(30);

        var session1 = new SessionInfo(sessionId, createdAt, expiresAt, 2, lastActivity);
        var session2 = new SessionInfo(sessionId, createdAt, expiresAt, 2, lastActivity);
        var session3 = new SessionInfo(Guid.NewGuid(), createdAt, expiresAt, 2, lastActivity);

        // Act & Assert
        session1.Should().Be(session2); // Same values
        session1.Should().NotBe(session3); // Different session ID
        (session1 == session2).Should().BeTrue();
        (session1 == session3).Should().BeFalse();
    }

    [Fact]
    public void ClipboardUpdatedEvent_Create_ShouldGenerateValidEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var content = ClipboardContent.Create("Test content", Guid.NewGuid());
        var deviceId = Guid.NewGuid();
        var previousHash = "previous-hash";

        // Act
        var eventModel = ClipboardUpdatedEvent.Create(sessionId, content, deviceId, previousHash);

        // Assert
        eventModel.EventId.Should().NotBe(Guid.Empty);
        eventModel.SessionId.Should().Be(sessionId);
        eventModel.Content.Should().Be(content);
        eventModel.UpdatedByDeviceId.Should().Be(deviceId);
        eventModel.PreviousContentHash.Should().Be(previousHash);
        eventModel.CorrelationId.Should().NotBeNullOrEmpty();
        eventModel.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DeviceJoinedEvent_Create_ShouldGenerateValidEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var device = new DeviceInfo(Guid.NewGuid(), "Test Device", DateTime.UtcNow, DateTime.UtcNow);
        var totalDeviceCount = 3;

        // Act
        var eventModel = DeviceJoinedEvent.Create(sessionId, device, totalDeviceCount);

        // Assert
        eventModel.EventId.Should().NotBe(Guid.Empty);
        eventModel.SessionId.Should().Be(sessionId);
        eventModel.Device.Should().Be(device);
        eventModel.TotalDeviceCount.Should().Be(totalDeviceCount);
        eventModel.CorrelationId.Should().NotBeNullOrEmpty();
        eventModel.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DeviceLeftEvent_Create_ShouldGenerateValidEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var deviceName = "Test Device";
        var totalDeviceCount = 2;
        var reason = DeviceLeaveReason.Disconnect;

        // Act
        var eventModel = DeviceLeftEvent.Create(sessionId, deviceId, deviceName, totalDeviceCount, reason);

        // Assert
        eventModel.EventId.Should().NotBe(Guid.Empty);
        eventModel.SessionId.Should().Be(sessionId);
        eventModel.DeviceId.Should().Be(deviceId);
        eventModel.DeviceName.Should().Be(deviceName);
        eventModel.TotalDeviceCount.Should().Be(totalDeviceCount);
        eventModel.Reason.Should().Be(reason);
        eventModel.CorrelationId.Should().NotBeNullOrEmpty();
        eventModel.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SessionEndedEvent_Create_ShouldGenerateValidEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var reason = SessionEndReason.Expired;
        var deviceCount = 0;

        // Act
        var eventModel = SessionEndedEvent.Create(sessionId, reason, deviceCount);

        // Assert
        eventModel.EventId.Should().NotBe(Guid.Empty);
        eventModel.SessionId.Should().Be(sessionId);
        eventModel.Reason.Should().Be(reason);
        eventModel.FinalDeviceCount.Should().Be(deviceCount);
        eventModel.CorrelationId.Should().NotBeNullOrEmpty();
        eventModel.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClipboardHistoryEntry_IsCurrent_ShouldDetectCurrentEntry()
    {
        // Arrange
        var content = ClipboardContent.Create("Test", Guid.NewGuid());
        var currentEntry = new ClipboardHistoryEntry(Guid.NewGuid(), content, 0);
        var oldEntry = new ClipboardHistoryEntry(Guid.NewGuid(), content, 1);

        // Act & Assert
        currentEntry.IsCurrent.Should().BeTrue();
        oldEntry.IsCurrent.Should().BeFalse();
    }
}
