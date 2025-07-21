using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Result of content validation.
/// </summary>
/// <param name="IsValid">Whether the content is valid</param>
/// <param name="ErrorMessage">Error message if validation failed</param>
/// <param name="ContentLength">Length of the content in characters</param>
/// <param name="ContentSizeBytes">Size of the content in bytes</param>
/// <param name="IsEmpty">Whether the content is empty or whitespace-only</param>
/// <param name="ContainsSensitiveData">Whether the content might contain sensitive data</param>
public sealed record ContentValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    int ContentLength = 0,
    int ContentSizeBytes = 0,
    bool IsEmpty = false,
    bool ContainsSensitiveData = false);

/// <summary>
/// Statistics about clipboard usage for a session.
/// </summary>
/// <param name="SessionId">The session ID</param>
/// <param name="TotalCopyOperations">Total number of copy operations</param>
/// <param name="TotalContentSize">Total size of all content copied (in bytes)</param>
/// <param name="LastUpdatedAt">When the clipboard was last updated</param>
/// <param name="LastUpdatedBy">Device that last updated the clipboard</param>
/// <param name="MostActiveDevice">Device with the most copy operations</param>
/// <param name="AverageContentSize">Average size of content per copy operation</param>
public sealed record ClipboardStatistics(
    Guid SessionId,
    int TotalCopyOperations,
    long TotalContentSize,
    DateTime? LastUpdatedAt,
    DeviceInfo? LastUpdatedBy,
    DeviceInfo? MostActiveDevice,
    double AverageContentSize);
