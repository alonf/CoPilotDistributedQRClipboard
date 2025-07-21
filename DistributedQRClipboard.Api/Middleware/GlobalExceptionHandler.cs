using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Api.Middleware;

/// <summary>
/// Global exception handler middleware that provides consistent error responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred while processing the request");

        var (statusCode, title, detail) = exception switch
        {
            SessionNotFoundException => (
                HttpStatusCode.NotFound,
                "Session Not Found",
                exception.Message
            ),
            InvalidSessionException => (
                HttpStatusCode.BadRequest,
                "Invalid Session",
                exception.Message
            ),
            ClipboardValidationException => (
                HttpStatusCode.BadRequest,
                "Validation Failed",
                exception.Message
            ),
            ClipboardException => (
                HttpStatusCode.BadRequest,
                "Clipboard Error",
                exception.Message
            ),
            QrCodeGenerationException => (
                HttpStatusCode.InternalServerError,
                "QR Code Generation Failed",
                "An error occurred while generating the QR code"
            ),
            OperationCanceledException => (
                HttpStatusCode.RequestTimeout,
                "Request Timeout",
                "The operation was cancelled due to timeout"
            ),
            ArgumentException => (
                HttpStatusCode.BadRequest,
                "Invalid Argument",
                exception.Message
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request"
            )
        };

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title,
            status = (int)statusCode,
            detail,
            instance = httpContext.Request.Path,
            traceId = httpContext.TraceIdentifier
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
