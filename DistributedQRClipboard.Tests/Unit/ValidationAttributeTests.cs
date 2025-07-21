using FluentAssertions;
using DistributedQRClipboard.Core.Validation;

namespace DistributedQRClipboard.Tests.Unit;

/// <summary>
/// Unit tests for validation attributes.
/// </summary>
public class ValidationAttributeTests
{
    [Fact]
    public void ValidSessionIdAttribute_ShouldValidateGuidCorrectly()
    {
        // Arrange
        var attribute = new ValidSessionIdAttribute();
        var validGuid = Guid.NewGuid();
        var emptyGuid = Guid.Empty;

        // Act & Assert
        attribute.IsValid(validGuid).Should().BeTrue();
        attribute.IsValid(emptyGuid).Should().BeFalse();
        attribute.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void ValidSessionIdAttribute_WithAllowEmpty_ShouldAllowEmptyGuid()
    {
        // Arrange
        var attribute = new ValidSessionIdAttribute { AllowEmpty = true };
        var validGuid = Guid.NewGuid();
        var emptyGuid = Guid.Empty;

        // Act & Assert
        attribute.IsValid(validGuid).Should().BeTrue();
        attribute.IsValid(emptyGuid).Should().BeTrue();
        attribute.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void ValidSessionIdAttribute_ShouldValidateStringGuidCorrectly()
    {
        // Arrange
        var attribute = new ValidSessionIdAttribute();
        var validGuidString = Guid.NewGuid().ToString();
        var emptyGuidString = Guid.Empty.ToString();
        var invalidGuidString = "not-a-guid";

        // Act & Assert
        attribute.IsValid(validGuidString).Should().BeTrue();
        attribute.IsValid(emptyGuidString).Should().BeFalse();
        attribute.IsValid(invalidGuidString).Should().BeFalse();
    }

    [Fact]
    public void ValidSessionIdAttribute_ShouldRejectInvalidTypes()
    {
        // Arrange
        var attribute = new ValidSessionIdAttribute();

        // Act & Assert
        attribute.IsValid(123).Should().BeFalse();
        attribute.IsValid(new object()).Should().BeFalse();
    }

    [Fact]
    public void ValidDeviceNameAttribute_ShouldValidateCorrectly()
    {
        // Arrange
        var attribute = new ValidDeviceNameAttribute();

        // Act & Assert
        attribute.IsValid("Valid Device").Should().BeTrue();
        attribute.IsValid("Device-123").Should().BeTrue();
        attribute.IsValid("Device_Name").Should().BeTrue();
        attribute.IsValid("A").Should().BeTrue();
        attribute.IsValid(new string('A', 50)).Should().BeTrue(); // Max length
        attribute.IsValid(null).Should().BeTrue(); // Null allowed for optional
        attribute.IsValid("").Should().BeTrue(); // Empty allowed for optional
    }

    [Fact]
    public void ValidDeviceNameAttribute_ShouldRejectInvalidNames()
    {
        // Arrange
        var attribute = new ValidDeviceNameAttribute();

        // Act & Assert
        attribute.IsValid(new string('A', 51)).Should().BeFalse(); // Too long
        attribute.IsValid("Device@Name").Should().BeFalse(); // Invalid character
        attribute.IsValid("Device#Name").Should().BeFalse(); // Invalid character
        attribute.IsValid("Device!Name").Should().BeFalse(); // Invalid character
        attribute.IsValid("Device$Name").Should().BeFalse(); // Invalid character
    }

    [Fact]
    public void ValidClipboardContentAttribute_ShouldValidateContentSize()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute();
        var validContent = "Valid content";
        var maxContent = new string('A', 10240); // Exactly at limit
        var tooLongContent = new string('A', 10241); // Over limit

        // Act & Assert
        attribute.IsValid(validContent).Should().BeTrue();
        attribute.IsValid(maxContent).Should().BeTrue();
        attribute.IsValid(tooLongContent).Should().BeFalse();
    }

    [Fact]
    public void ValidClipboardContentAttribute_ShouldDetectDangerousContent()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute();

        // Act & Assert
        attribute.IsValid("<script>alert('xss')</script>").Should().BeFalse();
        attribute.IsValid("javascript:alert('xss')").Should().BeFalse();
        attribute.IsValid("vbscript:msgbox('xss')").Should().BeFalse();
        attribute.IsValid("<img onload='alert(1)'>").Should().BeFalse();
        attribute.IsValid("data:text/html,<script>alert(1)</script>").Should().BeFalse();
        attribute.IsValid("Safe content").Should().BeTrue();
    }

    [Fact]
    public void ValidClipboardContentAttribute_WithAllowEmpty_ShouldAllowEmpty()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute { AllowEmpty = true };

        // Act & Assert
        attribute.IsValid("").Should().BeTrue();
        attribute.IsValid(null).Should().BeTrue();
        attribute.IsValid("Valid content").Should().BeTrue();
    }

    [Fact]
    public void ValidClipboardContentAttribute_WithoutAllowEmpty_ShouldRejectEmpty()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute { AllowEmpty = false };

        // Act & Assert
        attribute.IsValid("").Should().BeFalse();
        attribute.IsValid(null).Should().BeFalse();
        attribute.IsValid("Valid content").Should().BeTrue();
    }

    [Fact]
    public void ValidSessionExpirationAttribute_ShouldValidateMinutes()
    {
        // Arrange
        var attribute = new ValidSessionExpirationAttribute();

        // Act & Assert
        attribute.IsValid(1).Should().BeTrue(); // Min
        attribute.IsValid(60).Should().BeTrue(); // Common value
        attribute.IsValid(1440).Should().BeTrue(); // Max (24 hours)
        attribute.IsValid(0).Should().BeFalse(); // Below min
        attribute.IsValid(-1).Should().BeFalse(); // Negative
        attribute.IsValid(1441).Should().BeFalse(); // Above max
        attribute.IsValid(null).Should().BeFalse(); // Null
    }

    [Fact]
    public void ValidSessionExpirationAttribute_ShouldValidateTimeSpan()
    {
        // Arrange
        var attribute = new ValidSessionExpirationAttribute();

        // Act & Assert
        attribute.IsValid(TimeSpan.FromMinutes(1)).Should().BeTrue(); // Min
        attribute.IsValid(TimeSpan.FromHours(1)).Should().BeTrue(); // Common value
        attribute.IsValid(TimeSpan.FromHours(24)).Should().BeTrue(); // Max
        attribute.IsValid(TimeSpan.FromSeconds(30)).Should().BeFalse(); // Below min
        attribute.IsValid(TimeSpan.FromHours(25)).Should().BeFalse(); // Above max
    }

    [Fact]
    public void ValidContentHashAttribute_ShouldValidateBase64Hash()
    {
        // Arrange
        var attribute = new ValidContentHashAttribute();
        var validHash = Convert.ToBase64String(new byte[32]); // Valid 32-byte hash
        var shortHash = Convert.ToBase64String(new byte[16]); // Too short
        var invalidBase64 = "not-base64!@#";

        // Act & Assert
        attribute.IsValid(validHash).Should().BeTrue();
        attribute.IsValid(shortHash).Should().BeFalse();
        attribute.IsValid(invalidBase64).Should().BeFalse();
        attribute.IsValid("").Should().BeFalse();
        attribute.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void ValidCorrelationIdAttribute_ShouldValidateCorrectly()
    {
        // Arrange
        var attribute = new ValidCorrelationIdAttribute();

        // Act & Assert
        attribute.IsValid("valid-correlation-id").Should().BeTrue();
        attribute.IsValid("Valid_Correlation_123").Should().BeTrue();
        attribute.IsValid("A").Should().BeTrue(); // Min length
        attribute.IsValid(new string('A', 100)).Should().BeTrue(); // Max length
        attribute.IsValid("").Should().BeFalse(); // Empty
        attribute.IsValid("   ").Should().BeFalse(); // Whitespace
        attribute.IsValid(new string('A', 101)).Should().BeFalse(); // Too long
        attribute.IsValid("invalid@correlation").Should().BeFalse(); // Invalid character
        attribute.IsValid(null).Should().BeFalse(); // Null
    }

    [Fact]
    public void ValidationAttributes_ShouldFormatErrorMessagesCorrectly()
    {
        // Arrange
        var sessionIdAttribute = new ValidSessionIdAttribute();
        var deviceNameAttribute = new ValidDeviceNameAttribute();
        var contentAttribute = new ValidClipboardContentAttribute();
        var expirationAttribute = new ValidSessionExpirationAttribute();
        var hashAttribute = new ValidContentHashAttribute();
        var correlationAttribute = new ValidCorrelationIdAttribute();

        // Act & Assert
        sessionIdAttribute.FormatErrorMessage("SessionId").Should().Contain("SessionId");
        deviceNameAttribute.FormatErrorMessage("DeviceName").Should().Contain("DeviceName");
        contentAttribute.FormatErrorMessage("Content").Should().Contain("Content");
        expirationAttribute.FormatErrorMessage("Expiration").Should().Contain("Expiration");
        hashAttribute.FormatErrorMessage("Hash").Should().Contain("Hash");
        correlationAttribute.FormatErrorMessage("CorrelationId").Should().Contain("CorrelationId");
    }

    [Fact]
    public void ValidClipboardContentAttribute_ShouldHandleUnicodeContent()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute();
        var unicodeContent = "Hello 世界 🌍 Café résumé naïve";

        // Act & Assert
        attribute.IsValid(unicodeContent).Should().BeTrue();
    }

    [Fact]
    public void ValidClipboardContentAttribute_ShouldRespectByteLimit()
    {
        // Arrange
        var attribute = new ValidClipboardContentAttribute();
        
        // Create content that's under character limit but over byte limit due to Unicode
        var unicodeChar = "🌍"; // 4 bytes in UTF-8
        var charactersThatFitIn10KB = 10240 / 4; // 2560 characters
        var contentUnderByteLimit = string.Concat(Enumerable.Repeat(unicodeChar, charactersThatFitIn10KB));
        var contentOverByteLimit = string.Concat(Enumerable.Repeat(unicodeChar, charactersThatFitIn10KB + 1));

        // Act & Assert
        attribute.IsValid(contentUnderByteLimit).Should().BeTrue();
        attribute.IsValid(contentOverByteLimit).Should().BeFalse();
    }
}
