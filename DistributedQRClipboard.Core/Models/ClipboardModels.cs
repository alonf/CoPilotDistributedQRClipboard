using System.ComponentModel.DataAnnotations;

namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Represents immutable clipboard content with validation constraints.
/// </summary>
/// <param name="Content">The text content (max 10KB)</param>
/// <param name="CreatedAt">UTC timestamp when content was created</param>
/// <param name="DeviceId">ID of the device that created the content</param>
/// <param name="ContentHash">SHA-256 hash of the content for integrity verification</param>
public readonly record struct ClipboardContent(
    [StringLength(10240, ErrorMessage = "Content cannot exceed 10KB (10,240 characters)")]
    string Content,
    DateTime CreatedAt,
    Guid DeviceId,
    string ContentHash)
{
    /// <summary>
    /// Gets the content size in bytes (UTF-8 encoding).
    /// </summary>
    public readonly int SizeInBytes => System.Text.Encoding.UTF8.GetByteCount(Content);

    /// <summary>
    /// Indicates whether the content is empty or whitespace only.
    /// </summary>
    public readonly bool IsEmpty => string.IsNullOrWhiteSpace(Content);

    /// <summary>
    /// Gets a preview of the content (first 100 characters).
    /// </summary>
    public readonly string Preview => Content.Length <= 100 ? Content : Content[..100] + "...";

    /// <summary>
    /// Validates that the content hash matches the actual content.
    /// </summary>
    public readonly bool IsHashValid => ContentHash == ComputeHash(Content);

    /// <summary>
    /// Computes SHA-256 hash for the given content.
    /// </summary>
    /// <param name="content">Content to hash</param>
    /// <returns>Base64-encoded hash</returns>
    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Creates a new ClipboardContent with computed hash.
    /// </summary>
    /// <param name="content">The content to store</param>
    /// <param name="deviceId">The device ID creating the content</param>
    /// <returns>ClipboardContent with computed hash</returns>
    public static ClipboardContent Create(string content, Guid deviceId)
    {
        var hash = ComputeHash(content);
        return new ClipboardContent(content, DateTime.UtcNow, deviceId, hash);
    }
}

/// <summary>
/// Request model for copying content to clipboard.
/// </summary>
/// <param name="Content">The text content to copy</param>
/// <param name="SessionId">The session ID where content should be copied</param>
/// <param name="DeviceId">The device ID making the request</param>
public readonly record struct CopyToClipboardRequest(
    [Required(ErrorMessage = "Content is required")]
    [StringLength(10240, ErrorMessage = "Content cannot exceed 10KB")]
    string Content,
    [Required(ErrorMessage = "SessionId is required")]
    Guid SessionId,
    [Required(ErrorMessage = "DeviceId is required")]
    Guid DeviceId)
{
    /// <summary>
    /// Validates the request data.
    /// </summary>
    public readonly bool IsValid => !string.IsNullOrEmpty(Content) && 
                                   SessionId != Guid.Empty && 
                                   DeviceId != Guid.Empty &&
                                   Content.Length <= 10240;
}

/// <summary>
/// Response model for copy to clipboard operation.
/// </summary>
/// <param name="ClipboardContent">The stored clipboard content</param>
/// <param name="Success">Indicates whether the operation was successful</param>
/// <param name="ErrorMessage">Error message if operation failed</param>
public readonly record struct CopyToClipboardResponse(
    ClipboardContent? ClipboardContent,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Request model for getting clipboard content.
/// </summary>
/// <param name="SessionId">The session ID to get clipboard from</param>
/// <param name="DeviceId">The device ID making the request</param>
public readonly record struct GetClipboardRequest(
    [Required(ErrorMessage = "SessionId is required")]
    Guid SessionId,
    [Required(ErrorMessage = "DeviceId is required")]
    Guid DeviceId)
{
    /// <summary>
    /// Validates the request data.
    /// </summary>
    public readonly bool IsValid => SessionId != Guid.Empty && DeviceId != Guid.Empty;
}

/// <summary>
/// Response model for get clipboard operation.
/// </summary>
/// <param name="ClipboardContent">The current clipboard content</param>
/// <param name="Success">Indicates whether the operation was successful</param>
/// <param name="ErrorMessage">Error message if operation failed</param>
public readonly record struct GetClipboardResponse(
    ClipboardContent? ClipboardContent,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Represents clipboard history entry.
/// </summary>
/// <param name="Id">Unique identifier for the history entry</param>
/// <param name="Content">The clipboard content</param>
/// <param name="Index">Position in history (0 = most recent)</param>
public readonly record struct ClipboardHistoryEntry(
    Guid Id,
    ClipboardContent Content,
    int Index)
{
    /// <summary>
    /// Indicates whether this is the current (most recent) entry.
    /// </summary>
    public readonly bool IsCurrent => Index == 0;
}
