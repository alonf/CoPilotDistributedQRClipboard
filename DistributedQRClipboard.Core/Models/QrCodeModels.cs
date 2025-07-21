namespace DistributedQRClipboard.Core.Models;

/// <summary>
/// Error correction levels for QR code generation.
/// Higher levels provide better error recovery but result in larger QR codes.
/// </summary>
public enum QrCodeErrorCorrection
{
    /// <summary>
    /// Low error correction (~7% damage recovery)
    /// </summary>
    L = 0,

    /// <summary>
    /// Medium error correction (~15% damage recovery) - Default
    /// </summary>
    M = 1,

    /// <summary>
    /// Quartile error correction (~25% damage recovery)
    /// </summary>
    Q = 2,

    /// <summary>
    /// High error correction (~30% damage recovery)
    /// </summary>
    H = 3
}

/// <summary>
/// Request to generate a QR code for a session.
/// </summary>
/// <param name="SessionId">The session ID to generate a QR code for</param>
/// <param name="BaseUrl">Optional base URL override for the join link</param>
/// <param name="PixelsPerModule">Optional override for QR code size</param>
/// <param name="ErrorCorrectionLevel">Optional override for error correction level</param>
public sealed record GenerateQrCodeRequest(
    Guid SessionId,
    string? BaseUrl = null,
    int? PixelsPerModule = null,
    QrCodeErrorCorrection? ErrorCorrectionLevel = null
);

/// <summary>
/// Response containing generated QR code data.
/// </summary>
/// <param name="QrCodeBase64">Base64-encoded PNG image of the QR code</param>
/// <param name="JoinUrl">The URL encoded in the QR code</param>
/// <param name="SessionId">The session ID this QR code is for</param>
/// <param name="Success">Whether the QR code generation was successful</param>
/// <param name="ErrorMessage">Error message if generation failed</param>
public sealed record GenerateQrCodeResponse(
    string? QrCodeBase64,
    string? JoinUrl,
    Guid SessionId,
    bool Success,
    string? ErrorMessage = null
);

/// <summary>
/// Request to validate a QR code.
/// </summary>
/// <param name="QrCodeBase64">Base64-encoded QR code image to validate</param>
/// <param name="ExpectedSessionId">Expected session ID that should be encoded in the QR code</param>
public sealed record ValidateQrCodeRequest(
    string QrCodeBase64,
    Guid ExpectedSessionId
);

/// <summary>
/// Response from QR code validation.
/// </summary>
/// <param name="IsValid">Whether the QR code is valid and readable</param>
/// <param name="DecodedContent">The content decoded from the QR code</param>
/// <param name="ExtractedSessionId">Session ID extracted from the decoded URL (if any)</param>
/// <param name="Success">Whether the validation operation was successful</param>
/// <param name="ErrorMessage">Error message if validation failed</param>
public sealed record ValidateQrCodeResponse(
    bool IsValid,
    string? DecodedContent,
    Guid? ExtractedSessionId,
    bool Success,
    string? ErrorMessage = null
);

/// <summary>
/// Exception thrown when QR code generation fails.
/// </summary>
public sealed class QrCodeGenerationException : Exception
{
    /// <summary>
    /// The session ID that failed to generate a QR code.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initializes a new instance of the QrCodeGenerationException.
    /// </summary>
    /// <param name="sessionId">Session ID that failed</param>
    /// <param name="message">Error message</param>
    public QrCodeGenerationException(Guid sessionId, string message) : base(message)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// Initializes a new instance of the QrCodeGenerationException.
    /// </summary>
    /// <param name="sessionId">Session ID that failed</param>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public QrCodeGenerationException(Guid sessionId, string message, Exception innerException) : base(message, innerException)
    {
        SessionId = sessionId;
    }
}
