using System.ComponentModel.DataAnnotations;

namespace DistributedQRClipboard.Core.Validation;

/// <summary>
/// Validation attribute for session IDs.
/// </summary>
public sealed class ValidSessionIdAttribute : ValidationAttribute
{
    /// <summary>
    /// Gets or sets whether empty GUIDs are allowed.
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return false;

        if (value is Guid guid)
        {
            return AllowEmpty || guid != Guid.Empty;
        }

        if (value is string stringValue)
        {
            if (Guid.TryParse(stringValue, out var parsedGuid))
            {
                return AllowEmpty || parsedGuid != Guid.Empty;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be a valid GUID" + (AllowEmpty ? "." : " and cannot be empty.");
    }
}

/// <summary>
/// Validation attribute for device names.
/// </summary>
public sealed class ValidDeviceNameAttribute : ValidationAttribute
{
    /// <summary>
    /// Maximum length for device names.
    /// </summary>
    public const int MaxLength = 50;

    /// <summary>
    /// Minimum length for device names.
    /// </summary>
    public const int MinLength = 1;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        // Null or empty is allowed for optional device names
        if (value is null)
            return true;

        if (value is string stringValue)
        {
            // Empty strings are allowed for optional fields
            if (string.IsNullOrEmpty(stringValue))
                return true;

            // Validate length and characters
            if (stringValue.Length < MinLength || stringValue.Length > MaxLength)
                return false;

            // Only allow alphanumeric characters, spaces, hyphens, and underscores
            return stringValue.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_');
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between {MinLength} and {MaxLength} characters and contain only letters, numbers, spaces, hyphens, and underscores.";
    }
}

/// <summary>
/// Validation attribute for clipboard content.
/// </summary>
public sealed class ValidClipboardContentAttribute : ValidationAttribute
{
    /// <summary>
    /// Maximum content size in bytes (10KB).
    /// </summary>
    public const int MaxSizeBytes = 10240;

    /// <summary>
    /// Gets or sets whether empty content is allowed.
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return AllowEmpty;

        if (value is string stringValue)
        {
            // Check if empty content is allowed
            if (string.IsNullOrEmpty(stringValue))
                return AllowEmpty;

            // Check byte size
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(stringValue);
            if (byteCount > MaxSizeBytes)
                return false;

            // Check for potentially dangerous content patterns
            if (ContainsDangerousPatterns(stringValue))
                return false;

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must not exceed {MaxSizeBytes} bytes and cannot contain potentially dangerous content.";
    }

    /// <summary>
    /// Checks for potentially dangerous content patterns.
    /// </summary>
    /// <param name="content">Content to check.</param>
    /// <returns>True if dangerous patterns are found.</returns>
    private static bool ContainsDangerousPatterns(string content)
    {
        // Check for common script injection patterns
        var dangerousPatterns = new[]
        {
            "<script",
            "javascript:",
            "vbscript:",
            "onload=",
            "onerror=",
            "onclick=",
            "data:text/html"
        };

        return dangerousPatterns.Any(pattern => 
            content.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Validation attribute for session expiration times.
/// </summary>
public sealed class ValidSessionExpirationAttribute : ValidationAttribute
{
    /// <summary>
    /// Minimum session duration in minutes.
    /// </summary>
    public const int MinMinutes = 1;

    /// <summary>
    /// Maximum session duration in minutes (24 hours).
    /// </summary>
    public const int MaxMinutes = 1440;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return false;

        if (value is int intValue)
        {
            return intValue >= MinMinutes && intValue <= MaxMinutes;
        }

        if (value is TimeSpan timeSpanValue)
        {
            var minutes = (int)timeSpanValue.TotalMinutes;
            return minutes >= MinMinutes && minutes <= MaxMinutes;
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between {MinMinutes} and {MaxMinutes} minutes.";
    }
}

/// <summary>
/// Validation attribute for content hashes.
/// </summary>
public sealed class ValidContentHashAttribute : ValidationAttribute
{
    /// <summary>
    /// Expected length of a base64-encoded SHA-256 hash.
    /// </summary>
    public const int ExpectedHashLength = 44; // 32 bytes base64 encoded

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return false;

        if (value is string stringValue)
        {
            // Check length
            if (stringValue.Length != ExpectedHashLength)
                return false;

            // Check if it's valid base64
            try
            {
                var bytes = Convert.FromBase64String(stringValue);
                return bytes.Length == 32; // SHA-256 produces 32 bytes
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be a valid base64-encoded SHA-256 hash.";
    }
}

/// <summary>
/// Validation attribute for correlation IDs.
/// </summary>
public sealed class ValidCorrelationIdAttribute : ValidationAttribute
{
    /// <summary>
    /// Maximum length for correlation IDs.
    /// </summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Minimum length for correlation IDs.
    /// </summary>
    public const int MinLength = 1;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return false;

        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
                return false;

            if (stringValue.Length < MinLength || stringValue.Length > MaxLength)
                return false;

            // Allow alphanumeric characters, hyphens, and underscores
            return stringValue.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        return false;
    }

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field must be between {MinLength} and {MaxLength} characters and contain only letters, numbers, hyphens, and underscores.";
    }
}
