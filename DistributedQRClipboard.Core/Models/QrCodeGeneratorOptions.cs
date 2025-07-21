namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Configuration options for QR code generation service.
/// </summary>
public sealed class QrCodeGeneratorOptions
{
    /// <summary>
    /// Base URL for generating join links. Used when no explicit base URL is provided.
    /// Default: "https://localhost:5001"
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Path template for join URLs. {0} will be replaced with the session ID.
    /// Default: "/join/{0}"
    /// </summary>
    public string JoinUrlTemplate { get; set; } = "/join/{0}";

    /// <summary>
    /// Pixels per module for QR code generation. Controls the size of the generated QR code.
    /// Default: 10 pixels per module
    /// </summary>
    public int PixelsPerModule { get; set; } = 10;

    /// <summary>
    /// Default error correction level for QR codes.
    /// Default: Medium (M) - 15% damage recovery
    /// </summary>
    public QrCodeErrorCorrection DefaultErrorCorrectionLevel { get; set; } = QrCodeErrorCorrection.M;

    /// <summary>
    /// Whether to enable QR code caching for performance optimization.
    /// Default: true
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration time for generated QR codes in minutes.
    /// Default: 60 minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of concurrent QR code generation operations.
    /// Default: 10
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 10;

    /// <summary>
    /// Timeout for QR code generation operations in milliseconds.
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int GenerationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to include a border around the QR code.
    /// Default: true
    /// </summary>
    public bool IncludeBorder { get; set; } = true;

    /// <summary>
    /// Border size in modules when IncludeBorder is true.
    /// Default: 4 modules
    /// </summary>
    public int BorderSizeModules { get; set; } = 4;
}
