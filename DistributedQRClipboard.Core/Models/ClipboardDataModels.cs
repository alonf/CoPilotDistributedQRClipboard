using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Internal model for storing clipboard data in cache.
/// </summary>
/// <param name="SessionId">The session ID</param>
/// <param name="CurrentContent">Current clipboard content</param>
/// <param name="History">History of clipboard entries</param>
/// <param name="Statistics">Clipboard usage statistics</param>
/// <param name="CreatedAt">When this data was created</param>
/// <param name="LastUpdatedBy">Device that last updated the data</param>
public sealed record ClipboardData(
    Guid SessionId,
    ClipboardContent? CurrentContent,
    IList<ClipboardHistoryEntry> History,
    ClipboardStatistics Statistics,
    DateTime CreatedAt,
    DeviceInfo? LastUpdatedBy)
{
    /// <summary>
    /// Creates a new ClipboardData with updated content.
    /// </summary>
    public ClipboardData WithNewContent(ClipboardContent content, DeviceInfo updatedBy)
    {
        var now = DateTime.UtcNow;
        
        // Add current content to history if it exists
        var newHistory = new List<ClipboardHistoryEntry>(History);
        if (CurrentContent.HasValue)
        {
            var historyEntry = new ClipboardHistoryEntry(
                Guid.NewGuid(),
                CurrentContent.Value,
                0); // Will be reindexed
            newHistory.Insert(0, historyEntry);
        }

        // Reindex history entries and limit to reasonable size
        var indexedHistory = newHistory
            .Select((entry, index) => entry with { Index = index + 1 })
            .Take(50) // Keep last 50 entries
            .ToList();

        // Update statistics
        var newStats = Statistics with
        {
            TotalCopyOperations = Statistics.TotalCopyOperations + 1,
            TotalContentSize = Statistics.TotalContentSize + content.SizeInBytes,
            LastUpdatedAt = now,
            LastUpdatedBy = updatedBy,
            AverageContentSize = (double)(Statistics.TotalContentSize + content.SizeInBytes) / (Statistics.TotalCopyOperations + 1)
        };

        return this with
        {
            CurrentContent = content,
            History = indexedHistory,
            Statistics = newStats,
            LastUpdatedBy = updatedBy
        };
    }

    /// <summary>
    /// Clears the clipboard content.
    /// </summary>
    public ClipboardData WithClearedContent(DeviceInfo clearedBy)
    {
        var now = DateTime.UtcNow;
        
        return this with
        {
            CurrentContent = null,
            LastUpdatedBy = clearedBy
        };
    }
}

/// <summary>
/// Configuration options for the ClipboardManager service.
/// </summary>
public sealed class ClipboardManagerOptions
{
    /// <summary>
    /// Maximum content length in characters (default: 10KB).
    /// </summary>
    public int MaxContentLength { get; set; } = 10240;

    /// <summary>
    /// Maximum content size in bytes (default: 10KB).
    /// </summary>
    public int MaxContentSizeBytes { get; set; } = 10240;

    /// <summary>
    /// Clipboard cache expiration time in minutes (default: 60 minutes).
    /// </summary>
    public int ClipboardCacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of concurrent operations per session (default: 5).
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 5;

    /// <summary>
    /// Maximum number of history entries that can be requested at once (default: 50).
    /// </summary>
    public int MaxHistoryRequestLimit { get; set; } = 50;

    /// <summary>
    /// Whether to enable content sanitization (default: true).
    /// </summary>
    public bool EnableContentSanitization { get; set; } = true;

    /// <summary>
    /// Whether to enable sensitive data detection (default: true).
    /// </summary>
    public bool EnableSensitiveDataDetection { get; set; } = true;
}
