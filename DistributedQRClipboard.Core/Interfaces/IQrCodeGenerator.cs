using DistributedQRClipboard.Core.Models;

namespace DistributedQRClipboard.Core.Interfaces;

/// <summary>
/// Service for generating QR codes for session joining functionality.
/// Provides methods to generate QR codes containing session join URLs with proper error handling and optimization.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>
    /// Generates a QR code as base64-encoded image for the specified session.
    /// </summary>
    /// <param name="sessionId">The session ID to generate a QR code for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64-encoded PNG image of the QR code</returns>
    Task<string> GenerateQrCodeAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a join URL for the specified session.
    /// </summary>
    /// <param name="sessionId">The session ID to create a join URL for</param>
    /// <param name="baseUrl">Base URL of the application (optional, uses configured default if not provided)</param>
    /// <returns>Complete join URL for the session</returns>
    string GenerateJoinUrl(Guid sessionId, string? baseUrl = null);

    /// <summary>
    /// Validates that a generated QR code is readable and contains the expected content.
    /// </summary>
    /// <param name="qrCodeBase64">Base64-encoded QR code image to validate</param>
    /// <param name="expectedContent">Expected content that should be decoded from the QR code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if QR code is valid and contains expected content</returns>
    Task<bool> ValidateQrCodeAsync(string qrCodeBase64, string expectedContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a QR code with specific dimensions and error correction level.
    /// </summary>
    /// <param name="content">Content to encode in the QR code</param>
    /// <param name="pixelsPerModule">Pixels per module (controls size)</param>
    /// <param name="errorCorrectionLevel">Error correction level for the QR code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64-encoded PNG image of the QR code</returns>
    Task<string> GenerateQrCodeAsync(string content, int pixelsPerModule = 10, QrCodeErrorCorrection errorCorrectionLevel = QrCodeErrorCorrection.M, CancellationToken cancellationToken = default);
}
