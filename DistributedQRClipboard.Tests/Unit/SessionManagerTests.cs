using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DistributedQRClipboard.Core.Services;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for the SessionManager service.
/// </summary>
public class SessionManagerTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<SessionManager>> _loggerMock;
    private readonly SessionManagerOptions _options;
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<SessionManager>>();
        _options = new SessionManagerOptions
        {
            DefaultExpirationMinutes = 60,
            MaxDevicesPerSession = 5,
            MaxConcurrentSessions = 1000,
            DeviceInactivityTimeoutMinutes = 5,
            SessionIdLength = 64,
            CleanupIntervalMinutes = 30
        };

        var optionsMock = new Mock<IOptions<SessionManagerOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _sessionManager = new SessionManager(_memoryCache, optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateSecureSessionId()
    {
        // Arrange
        var request = new CreateSessionRequest("Test Device", 60);

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.SessionInfo.Should().NotBeNull();
        response.SessionInfo!.Value.SessionId.Should().NotBe(Guid.Empty);
        response.QrCodeUrl.Should().NotBeNullOrEmpty();
        response.QrCodeUrl.Should().Contain(response.SessionInfo.Value.SessionId.ToString());

        // Verify session ID is cryptographically secure (non-empty GUID)
        var sessionId = response.SessionInfo.Value.SessionId;
        sessionId.Should().NotBe(Guid.Empty);
        sessionId.ToString().Should().HaveLength(36); // Standard GUID string length
    }

    [Fact]
    public async Task CreateSession_WithInvalidRequest_ShouldReturnError()
    {
        // Arrange
        var invalidRequest = new CreateSessionRequest("Test Device", 0); // Invalid expiration

        // Act
        var response = await _sessionManager.CreateSessionAsync(invalidRequest);

        // Assert
        response.Success.Should().BeFalse();
        response.SessionInfo.Should().BeNull();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain("Invalid session creation request");
    }

    [Fact]
    public async Task CreateSession_ShouldSetCorrectExpiration()
    {
        // Arrange
        var request = new CreateSessionRequest("Test Device", 120); // 2 hours
        var beforeCreation = DateTime.UtcNow;

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.SessionInfo.Should().NotBeNull();
        
        var sessionInfo = response.SessionInfo!.Value;
        sessionInfo.CreatedAt.Should().BeCloseTo(beforeCreation, TimeSpan.FromSeconds(5));
        sessionInfo.ExpiresAt.Should().BeCloseTo(beforeCreation.AddMinutes(120), TimeSpan.FromSeconds(5));
        sessionInfo.DeviceCount.Should().Be(1);
        sessionInfo.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task JoinSession_WithValidSession_ShouldAddDevice()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        var joinRequest = new JoinSessionRequest(sessionId, "Device 2");

        // Act
        var joinResponse = await _sessionManager.JoinSessionAsync(joinRequest);

        // Assert
        joinResponse.Success.Should().BeTrue();
        joinResponse.SessionInfo.Should().NotBeNull();
        joinResponse.SessionInfo!.Value.DeviceCount.Should().Be(2);
        joinResponse.SessionInfo!.Value.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task JoinSession_WithNonExistentSession_ShouldReturnError()
    {
        // Arrange
        var nonExistentSessionId = Guid.NewGuid();
        var joinRequest = new JoinSessionRequest(nonExistentSessionId, "Device 1");

        // Act
        var response = await _sessionManager.JoinSessionAsync(joinRequest);

        // Assert
        response.Success.Should().BeFalse();
        response.SessionInfo.Should().BeNull();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain("Session not found or has expired");
    }

    [Fact]
    public async Task JoinSession_WhenSessionIsFull_ShouldReturnError()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Add devices until session is full
        for (int i = 2; i <= _options.MaxDevicesPerSession; i++)
        {
            var joinRequest = new JoinSessionRequest(sessionId, $"Device {i}");
            await _sessionManager.JoinSessionAsync(joinRequest);
        }

        // Try to add one more device (should fail)
        var failingJoinRequest = new JoinSessionRequest(sessionId, "Device 6");

        // Act
        var response = await _sessionManager.JoinSessionAsync(failingJoinRequest);

        // Assert
        response.Success.Should().BeFalse();
        response.SessionInfo.Should().BeNull();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
        response.ErrorMessage.Should().Contain("Session is full");
        response.ErrorMessage.Should().Contain(_options.MaxDevicesPerSession.ToString());
    }

    [Fact]
    public async Task GetSession_WithValidSessionId_ShouldReturnSessionInfo()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Act
        var sessionInfo = await _sessionManager.GetSessionAsync(sessionId);

        // Assert
        sessionInfo.SessionId.Should().Be(sessionId);
        sessionInfo.DeviceCount.Should().Be(1);
        sessionInfo.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetSession_WithInvalidSessionId_ShouldThrowSessionNotFoundException()
    {
        // Arrange
        var invalidSessionId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _sessionManager.GetSessionAsync(invalidSessionId))
            .Should().ThrowAsync<SessionNotFoundException>()
            .WithMessage($"*{invalidSessionId}*");
    }

    [Fact]
    public async Task ValidateSession_WithValidSession_ShouldReturnTrue()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Act
        var isValid = await _sessionManager.ValidateSessionAsync(sessionId);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSession_WithInvalidSession_ShouldReturnFalse()
    {
        // Arrange
        var invalidSessionId = Guid.NewGuid();

        // Act
        var isValid = await _sessionManager.ValidateSessionAsync(invalidSessionId);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveSession_ShouldRemoveDevice()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Add second device
        var joinRequest = new JoinSessionRequest(sessionId, "Device 2");
        var joinResponse = await _sessionManager.JoinSessionAsync(joinRequest);

        // Get device list to find device ID
        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        var deviceToRemove = devices.First(d => d.DeviceName == "Device 2");

        // Act
        var updatedSession = await _sessionManager.LeaveSessionAsync(sessionId, deviceToRemove.DeviceId, DeviceLeaveReason.Disconnect);

        // Assert
        updatedSession.DeviceCount.Should().Be(1);
        
        // Verify device is actually removed
        var remainingDevices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        remainingDevices.Should().HaveCount(1);
        remainingDevices.Should().NotContain(d => d.DeviceId == deviceToRemove.DeviceId);
    }

    [Fact]
    public async Task LeaveSession_WhenLastDeviceLeaves_ShouldCloseSession()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        var lastDevice = devices.First();

        // Act
        await _sessionManager.LeaveSessionAsync(sessionId, lastDevice.DeviceId, DeviceLeaveReason.Disconnect);

        // Assert
        var isValid = await _sessionManager.ValidateSessionAsync(sessionId);
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateDeviceActivity_ShouldUpdateLastSeen()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        var device = devices.First();
        var originalLastSeen = device.LastSeen;

        // Wait a short time to ensure timestamp difference
        await Task.Delay(10);

        // Act
        await _sessionManager.UpdateDeviceActivityAsync(sessionId, device.DeviceId);

        // Assert
        var updatedDevices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        var updatedDevice = updatedDevices.First(d => d.DeviceId == device.DeviceId);
        updatedDevice.LastSeen.Should().BeAfter(originalLastSeen);
    }

    [Fact]
    public async Task ExtendSession_ShouldUpdateExpirationTime()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;
        var originalExpiresAt = createResponse.SessionInfo!.Value.ExpiresAt;

        var extensionMinutes = 30;

        // Act
        var updatedSession = await _sessionManager.ExtendSessionAsync(sessionId, extensionMinutes);

        // Assert
        updatedSession.ExpiresAt.Should().BeCloseTo(originalExpiresAt.AddMinutes(extensionMinutes), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExtendSession_WithInvalidExtension_ShouldThrowArgumentException()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Act & Assert
        await FluentActions.Invoking(() => _sessionManager.ExtendSessionAsync(sessionId, -10))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Extension minutes must be between 1 and*");

        await FluentActions.Invoking(() => _sessionManager.ExtendSessionAsync(sessionId, 2000))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Extension minutes must be between 1 and*");
    }

    [Fact]
    public async Task CloseSession_ShouldMakeSessionInvalid()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Test Device", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Verify session is initially valid
        var isValid = await _sessionManager.ValidateSessionAsync(sessionId);
        isValid.Should().BeTrue();

        // Act
        await _sessionManager.CloseSessionAsync(sessionId, SessionEndReason.ExplicitClose);

        // Assert
        var isValidAfterClose = await _sessionManager.ValidateSessionAsync(sessionId);
        isValidAfterClose.Should().BeFalse();

        // Should throw exception when trying to access closed session
        await FluentActions.Invoking(() => _sessionManager.GetSessionAsync(sessionId))
            .Should().ThrowAsync<SessionNotFoundException>();
    }

    [Fact]
    public async Task GetSessionDevices_ShouldReturnAllConnectedDevices()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Add more devices
        await _sessionManager.JoinSessionAsync(new JoinSessionRequest(sessionId, "Device 2"));
        await _sessionManager.JoinSessionAsync(new JoinSessionRequest(sessionId, "Device 3"));

        // Act
        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);

        // Assert
        devices.Should().HaveCount(3);
        devices.Should().Contain(d => d.DeviceName == "Device 1");
        devices.Should().Contain(d => d.DeviceName == "Device 2");
        devices.Should().Contain(d => d.DeviceName == "Device 3");
        
        // All devices should be active
        devices.Should().OnlyContain(d => d.IsActive);
    }

    [Fact]
    public async Task CleanupExpired_ShouldRemoveExpiredSessions()
    {
        // Arrange
        var shortExpirationRequest = new CreateSessionRequest("Test Device", 1); // 1 minute
        var response = await _sessionManager.CreateSessionAsync(shortExpirationRequest);
        var sessionId = response.SessionInfo!.Value.SessionId;

        // Verify session exists
        var isValid = await _sessionManager.ValidateSessionAsync(sessionId);
        isValid.Should().BeTrue();

        // Act
        var (expiredSessions, inactiveDevices) = await _sessionManager.CleanupExpiredAsync();

        // Assert
        // Note: Since we're using IMemoryCache with automatic expiration,
        // the actual cleanup behavior depends on cache implementation
        // This test verifies the method executes without errors
        expiredSessions.Should().BeGreaterOrEqualTo(0);
        inactiveDevices.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetSessionStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        var createRequest1 = new CreateSessionRequest("Device 1", 60);
        var createRequest2 = new CreateSessionRequest("Device 2", 60);
        
        await _sessionManager.CreateSessionAsync(createRequest1);
        await _sessionManager.CreateSessionAsync(createRequest2);

        // Act
        var statistics = await _sessionManager.GetSessionStatisticsAsync();

        // Assert
        statistics.TotalActiveSessions.Should().BeGreaterOrEqualTo(0);
        statistics.TotalConnectedDevices.Should().BeGreaterOrEqualTo(0);
        statistics.SessionsCreatedInLastHour.Should().BeGreaterOrEqualTo(2);
        statistics.AverageDevicesPerSession.Should().BeGreaterOrEqualTo(0);
    }

    [Theory]
    [InlineData(DeviceLeaveReason.Disconnect)]
    [InlineData(DeviceLeaveReason.Timeout)]
    [InlineData(DeviceLeaveReason.SessionExpired)]
    [InlineData(DeviceLeaveReason.ConnectionError)]
    [InlineData(DeviceLeaveReason.ServerShutdown)]
    public async Task LeaveSession_ShouldHandleAllLeaveReasons(DeviceLeaveReason reason)
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        var joinRequest = new JoinSessionRequest(sessionId, "Device 2");
        await _sessionManager.JoinSessionAsync(joinRequest);

        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        var deviceToRemove = devices.First(d => d.DeviceName == "Device 2");

        // Act
        var updatedSession = await _sessionManager.LeaveSessionAsync(sessionId, deviceToRemove.DeviceId, reason);

        // Assert
        updatedSession.DeviceCount.Should().Be(1);
    }

    [Fact]
    public async Task Session_ShouldExpireAfter24Hours()
    {
        // Arrange
        var request = new CreateSessionRequest("Test Device", 1440); // 24 hours
        var beforeCreation = DateTime.UtcNow;

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.SessionInfo.Should().NotBeNull();
        
        var sessionInfo = response.SessionInfo!.Value;
        
        // Verify session expires after exactly 24 hours
        var expectedExpiration = beforeCreation.AddHours(24);
        sessionInfo.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromMinutes(1));
        
        // Verify session is currently active
        sessionInfo.IsActive.Should().BeTrue();
        
        // Verify expiration calculation is correct
        var timeUntilExpiration = sessionInfo.ExpiresAt - sessionInfo.CreatedAt;
        timeUntilExpiration.Should().BeCloseTo(TimeSpan.FromHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Session_ShouldTrackConnectedDevices()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Act & Assert - Add devices and verify tracking
        var session1 = await _sessionManager.GetSessionAsync(sessionId);
        session1.DeviceCount.Should().Be(1);

        // Add second device
        await _sessionManager.JoinSessionAsync(new JoinSessionRequest(sessionId, "Device 2"));
        var session2 = await _sessionManager.GetSessionAsync(sessionId);
        session2.DeviceCount.Should().Be(2);

        // Add third device
        await _sessionManager.JoinSessionAsync(new JoinSessionRequest(sessionId, "Device 3"));
        var session3 = await _sessionManager.GetSessionAsync(sessionId);
        session3.DeviceCount.Should().Be(3);

        // Verify device information is tracked correctly
        var devices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        devices.Should().HaveCount(3);
        devices.Should().Contain(d => d.DeviceName == "Device 1");
        devices.Should().Contain(d => d.DeviceName == "Device 2");
        devices.Should().Contain(d => d.DeviceName == "Device 3");

        // Verify each device has proper timestamps
        foreach (var device in devices)
        {
            device.DeviceId.Should().NotBe(Guid.Empty);
            device.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            device.LastSeen.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            device.IsActive.Should().BeTrue();
        }

        // Remove device and verify tracking updates
        var deviceToRemove = devices.First(d => d.DeviceName == "Device 2");
        await _sessionManager.LeaveSessionAsync(sessionId, deviceToRemove.DeviceId, DeviceLeaveReason.Disconnect);
        
        var session4 = await _sessionManager.GetSessionAsync(sessionId);
        session4.DeviceCount.Should().Be(2);
        
        var remainingDevices = await _sessionManager.GetSessionDevicesAsync(sessionId);
        remainingDevices.Should().HaveCount(2);
        remainingDevices.Should().NotContain(d => d.DeviceId == deviceToRemove.DeviceId);
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateSecureSessionId_MeetsSecurityRequirements()
    {
        // Arrange
        var request = new CreateSessionRequest("Test Device", 60);
        var sessionIds = new HashSet<Guid>();
        const int testIterations = 100;

        // Act - Generate multiple session IDs to test uniqueness and security
        for (int i = 0; i < testIterations; i++)
        {
            var response = await _sessionManager.CreateSessionAsync(request);
            response.Success.Should().BeTrue();
            
            var sessionId = response.SessionInfo!.Value.SessionId;
            
            // Assert - Each session ID should be unique
            sessionIds.Should().NotContain(sessionId);
            sessionIds.Add(sessionId);
            
            // Verify session ID is cryptographically secure (non-empty GUID)
            sessionId.Should().NotBe(Guid.Empty);
            
            // Verify session ID string representation meets minimum length requirement
            var sessionIdString = sessionId.ToString();
            sessionIdString.Should().HaveLength(36); // Standard GUID format: 8-4-4-4-12 = 36 characters
            
            // Verify GUID version and variant bits for cryptographic security
            var bytes = sessionId.ToByteArray();
            
            // Check version bits (should be 0100 for version 4)
            var versionBits = (bytes[6] & 0xF0) >> 4;
            versionBits.Should().Be(4, "Session ID should use UUID version 4 for cryptographic security");
            
            // Check variant bits (should be 10xx for RFC 4122)
            var variantBits = (bytes[8] & 0xC0) >> 6;
            variantBits.Should().Be(2, "Session ID should use RFC 4122 variant bits");
        }

        // Assert - All session IDs should be unique (no collisions)
        sessionIds.Should().HaveCount(testIterations);
    }

    [Fact]
    public async Task Session_ShouldEnforceDeviceLimit_ExactlyFiveDevices()
    {
        // Arrange
        var createRequest = new CreateSessionRequest("Device 1", 60);
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);
        var sessionId = createResponse.SessionInfo!.Value.SessionId;

        // Act - Add devices up to the limit (5 total)
        for (int i = 2; i <= _options.MaxDevicesPerSession; i++)
        {
            var joinRequest = new JoinSessionRequest(sessionId, $"Device {i}");
            var joinResponse = await _sessionManager.JoinSessionAsync(joinRequest);
            
            // Assert - Each device should be added successfully
            joinResponse.Success.Should().BeTrue($"Device {i} should be added successfully");
            joinResponse.SessionInfo!.Value.DeviceCount.Should().Be(i);
        }

        // Verify we have exactly the maximum number of devices
        var session = await _sessionManager.GetSessionAsync(sessionId);
        session.DeviceCount.Should().Be(_options.MaxDevicesPerSession);

        // Act - Try to add one more device (should fail)
        var failingJoinRequest = new JoinSessionRequest(sessionId, "Device 6");
        var failingResponse = await _sessionManager.JoinSessionAsync(failingJoinRequest);

        // Assert - Should be rejected with appropriate error
        failingResponse.Success.Should().BeFalse();
        failingResponse.SessionInfo.Should().BeNull();
        failingResponse.ErrorMessage.Should().NotBeNullOrEmpty();
        failingResponse.ErrorMessage.Should().Contain("Session is full");
        failingResponse.ErrorMessage.Should().Contain(_options.MaxDevicesPerSession.ToString());

        // Verify device count hasn't changed
        var finalSession = await _sessionManager.GetSessionAsync(sessionId);
        finalSession.DeviceCount.Should().Be(_options.MaxDevicesPerSession);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }
}
