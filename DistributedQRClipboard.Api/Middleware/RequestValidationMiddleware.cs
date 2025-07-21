using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DistributedQRClipboard.Api.Middleware;

/// <summary>
/// Middleware for validating request models and handling validation errors.
/// </summary>
public sealed class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add correlation ID to the response headers
        var correlationId = context.TraceIdentifier;
        context.Response.Headers.TryAdd("X-Correlation-ID", correlationId);

        // Log the incoming request
        _logger.LogDebug("Processing {Method} request to {Path} with correlation ID {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for request {Method} {Path}", 
                context.Request.Method, context.Request.Path);

            await HandleValidationException(context, ex);
        }
    }

    private static async Task HandleValidationException(HttpContext context, ValidationException ex)
    {
        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title = "Validation Failed",
            status = StatusCodes.Status400BadRequest,
            detail = ex.Message,
            instance = context.Request.Path,
            traceId = context.TraceIdentifier
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension methods for registering request validation middleware.
/// </summary>
public static class RequestValidationMiddlewareExtensions
{
    /// <summary>
    /// Adds request validation middleware to the pipeline.
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder</returns>
    public static IApplicationBuilder UseRequestValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestValidationMiddleware>();
    }
}
