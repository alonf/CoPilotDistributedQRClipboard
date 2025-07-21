using FluentAssertions;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for custom exception classes.
/// </summary>
public class ExceptionTests
{
    [Fact]
    public void SessionNotFoundException_ShouldContainSessionId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var exception = new SessionNotFoundException(sessionId);

        // Assert
        exception.SessionId.Should().Be(sessionId);
        exception.ErrorCode.Should().Be("SESSION_NOT_FOUND");
        exception.Message.Should().Contain(sessionId.ToString());
        exception.Context.Should().ContainKey("SessionId");
        exception.Context["SessionId"].Should().Be(sessionId);
    }

    [Fact]
    public void SessionNotFoundException_WithCustomMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var customMessage = "Custom error message";

        // Act
        var exception = new SessionNotFoundException(sessionId, customMessage);

        // Assert
        exception.SessionId.Should().Be(sessionId);
        exception.Message.Should().Be(customMessage);
        exception.Context.Should().ContainKey("SessionId");
    }

    [Fact]
    public void SessionNotFoundException_WithInnerException_ShouldPreserveInnerException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var innerException = new ArgumentException("Inner exception");
        var customMessage = "Custom error message";

        // Act
        var exception = new SessionNotFoundException(sessionId, customMessage, innerException);

        // Assert
        exception.SessionId.Should().Be(sessionId);
        exception.Message.Should().Be(customMessage);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void InvalidSessionException_ShouldContainSessionIdAndReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var reason = SessionInvalidReason.Expired;

        // Act
        var exception = new InvalidSessionException(sessionId, reason);

        // Assert
        exception.SessionId.Should().Be(sessionId);
        exception.Reason.Should().Be(reason);
        exception.ErrorCode.Should().Be("INVALID_SESSION");
        exception.Message.Should().Contain(sessionId.ToString());
        exception.Message.Should().Contain(reason.ToString());
        exception.Context.Should().ContainKey("SessionId");
        exception.Context.Should().ContainKey("Reason");
        exception.Context["SessionId"].Should().Be(sessionId);
        exception.Context["Reason"].Should().Be(reason);
    }

    [Fact]
    public void ClipboardValidationException_WithSingleError_ShouldStoreError()
    {
        // Arrange
        var validationError = "Content is too long";

        // Act
        var exception = new ClipboardValidationException(validationError);

        // Assert
        exception.ValidationErrors.Should().ContainSingle();
        exception.ValidationErrors.First().Should().Be(validationError);
        exception.ErrorCode.Should().Be("CLIPBOARD_VALIDATION_FAILED");
        exception.Context.Should().ContainKey("ValidationErrors");
    }

    [Fact]
    public void ClipboardValidationException_WithMultipleErrors_ShouldStoreAllErrors()
    {
        // Arrange
        var validationErrors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var exception = new ClipboardValidationException(validationErrors);

        // Assert
        exception.ValidationErrors.Should().HaveCount(3);
        exception.ValidationErrors.Should().BeEquivalentTo(validationErrors);
        exception.Context.Should().ContainKey("ValidationErrors");
    }

    [Fact]
    public void ClipboardValidationException_WithContentPreview_ShouldTruncateContent()
    {
        // Arrange
        var validationError = "Content is invalid";
        var longContent = new string('A', 150);

        // Act
        var exception = new ClipboardValidationException(validationError, longContent);

        // Assert
        exception.ValidationErrors.Should().ContainSingle();
        exception.ContentPreview.Should().HaveLength(103); // 100 chars + "..."
        exception.ContentPreview.Should().EndWith("...");
        exception.Context.Should().ContainKey("ContentPreview");
    }

    [Fact]
    public void ClipboardValidationException_WithShortContent_ShouldNotTruncate()
    {
        // Arrange
        var validationError = "Content is invalid";
        var shortContent = "Short content";

        // Act
        var exception = new ClipboardValidationException(validationError, shortContent);

        // Assert
        exception.ContentPreview.Should().Be(shortContent);
        exception.ContentPreview.Should().NotContain("...");
    }

    [Fact]
    public void DeviceOperationException_ShouldContainDeviceIdAndOperation()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var operation = "JoinSession";

        // Act
        var exception = new DeviceOperationException(deviceId, operation);

        // Assert
        exception.DeviceId.Should().Be(deviceId);
        exception.Operation.Should().Be(operation);
        exception.ErrorCode.Should().Be("DEVICE_OPERATION_FAILED");
        exception.Message.Should().Contain(deviceId.ToString());
        exception.Message.Should().Contain(operation);
        exception.Context.Should().ContainKey("DeviceId");
        exception.Context.Should().ContainKey("Operation");
        exception.Context["DeviceId"].Should().Be(deviceId);
        exception.Context["Operation"].Should().Be(operation);
    }

    [Fact]
    public void ClipboardException_WithContext_ShouldSupportMethodChaining()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var exception = new SessionNotFoundException(sessionId)
            .WithContext("AdditionalInfo", "Test value")
            .WithContext("RequestId", "12345");

        // Assert
        exception.Context.Should().ContainKey("SessionId");
        exception.Context.Should().ContainKey("AdditionalInfo");
        exception.Context.Should().ContainKey("RequestId");
        exception.Context["AdditionalInfo"].Should().Be("Test value");
        exception.Context["RequestId"].Should().Be("12345");
    }

    [Fact]
    public void ClipboardException_Context_ShouldAllowOverwriting()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var exception = new SessionNotFoundException(sessionId);

        // Act
        exception.WithContext("TestKey", "Original Value");
        exception.WithContext("TestKey", "Updated Value");

        // Assert
        exception.Context["TestKey"].Should().Be("Updated Value");
    }

    [Theory]
    [InlineData(SessionInvalidReason.Expired)]
    [InlineData(SessionInvalidReason.MaxCapacityReached)]
    [InlineData(SessionInvalidReason.Closed)]
    [InlineData(SessionInvalidReason.Terminated)]
    [InlineData(SessionInvalidReason.InvalidFormat)]
    [InlineData(SessionInvalidReason.Unauthorized)]
    public void InvalidSessionException_ShouldHandleAllReasons(SessionInvalidReason reason)
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var exception = new InvalidSessionException(sessionId, reason);

        // Assert
        exception.Reason.Should().Be(reason);
        exception.Message.Should().Contain(reason.ToString());
    }

    [Fact]
    public void ValidationErrors_ShouldBeReadOnly()
    {
        // Arrange
        var validationErrors = new[] { "Error 1", "Error 2" };
        var exception = new ClipboardValidationException(validationErrors);

        // Act & Assert
        exception.ValidationErrors.Should().BeAssignableTo<IReadOnlyList<string>>();
        
        // This should not compile if ValidationErrors is mutable
        // exception.ValidationErrors.Add("Error 3"); // Should cause compilation error
    }
}
