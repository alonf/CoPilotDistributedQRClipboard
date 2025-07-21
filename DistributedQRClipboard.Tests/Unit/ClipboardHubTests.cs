using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using DistributedQRClipboard.Api.Hubs;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for ClipboardHub SignalR implementation.
/// Tests focus on business logic and validate hub method behavior.
/// SignalR client notification testing is left for integration tests.
/// </summary>
public sealed class ClipboardHubTests
{
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<IClipboardManager> _mockClipboardManager;
    private readonly Mock<ILogger<ClipboardHub>> _mockLogger;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly ClipboardHub _hub;

    public ClipboardHubTests()
    {
        _mockSessionManager = new Mock<ISessionManager>();
        _mockClipboardManager = new Mock<IClipboardManager>();
        _mockLogger = new Mock<ILogger<ClipboardHub>>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();

        _hub = new ClipboardHub(_mockSessionManager.Object, _mockClipboardManager.Object, _mockLogger.Object);
        
        // Set up the hub context
        _hub.Context = _mockContext.Object;
        _hub.Clients = _mockClients.Object;
        _hub.Groups = _mockGroups.Object;

        // Set up basic context properties
        _mockContext.Setup(x => x.ConnectionId).Returns("test-connection-id");
    }

    /// <summary>
    /// Tests the core validation logic for invalid session IDs.
    /// </summary>
    [Fact]
    public async Task JoinSessionAsync_WithInvalidSessionId_ShouldReturnFailure()
    {
        // Arrange
        var invalidSessionId = "invalid-session-id";
        var deviceId = Guid.NewGuid().ToString();

        // Act
        var result = await _hub.JoinSessionAsync(invalidSessionId, deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid session ID format");
    }

    /// <summary>
    /// Tests the core validation logic for invalid device IDs.
    /// </summary>
    [Fact]
    public async Task JoinSessionAsync_WithInvalidDeviceId_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var invalidDeviceId = "invalid-device-id";

        // Act
        var result = await _hub.JoinSessionAsync(sessionId, invalidDeviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid device ID format");
    }

    /// <summary>
    /// Tests the core clipboard content retrieval logic.
    /// </summary>
    [Fact]
    public async Task GetClipboardContentAsync_WithValidData_ShouldReturnContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var clipboardContent = new ClipboardContent("Test content", DateTime.UtcNow, deviceId, "hash123");

        var getResult = new GetClipboardResponse(clipboardContent, true, null);
        _mockClipboardManager.Setup(x => x.GetClipboardAsync(It.IsAny<GetClipboardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResult);

        // Act
        var result = await _hub.GetClipboardContentAsync(sessionId.ToString(), deviceId.ToString());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ClipboardContent.Should().NotBeNull();
        result.ClipboardContent!.Value.Content.Should().Be("Test content");

        // Verify clipboard manager was called
        _mockClipboardManager.Verify(x => x.GetClipboardAsync(It.IsAny<GetClipboardRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests the core clipboard error handling logic.
    /// </summary>
    [Fact]
    public async Task GetClipboardContentAsync_WithClipboardError_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        _mockClipboardManager.Setup(x => x.GetClipboardAsync(It.IsAny<GetClipboardRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        var result = await _hub.GetClipboardContentAsync(sessionId.ToString(), deviceId.ToString());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("An error occurred while retrieving clipboard content");
    }
}
