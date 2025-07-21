using Microsoft.AspNetCore.Mvc;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Api.Endpoints;

/// <summary>
/// Clipboard management endpoints for copying and retrieving content.
/// </summary>
public static class ClipboardEndpoints
{
    /// <summary>
    /// Configures clipboard-related endpoints.
    /// </summary>
    /// <param name="app">The web application</param>
    public static void MapClipboardEndpoints(this WebApplication app)
    {
        var clipboardGroup = app.MapGroup("/api/sessions/{sessionId:guid}/clipboard")
            .WithTags("Clipboard")
            .WithOpenApi();

        // Copy content to clipboard
        clipboardGroup.MapPost("/", CopyToClipboardAsync)
            .WithName("CopyToClipboard")
            .WithSummary("Copy content to shared clipboard")
            .WithDescription("Copies text content to the shared clipboard for the specified session")
            .Produces<CopyToClipboardResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Get clipboard content
        clipboardGroup.MapGet("/", GetClipboardAsync)
            .WithName("GetClipboard")
            .WithSummary("Get clipboard content")
            .WithDescription("Retrieves the current content from the shared clipboard")
            .Produces<GetClipboardResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Clear clipboard content
        clipboardGroup.MapDelete("/", ClearClipboardAsync)
            .WithName("ClearClipboard")
            .WithSummary("Clear clipboard content")
            .WithDescription("Clears all content from the shared clipboard")
            .Produces<MessageResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Get clipboard history
        clipboardGroup.MapGet("/history", GetClipboardHistoryAsync)
            .WithName("GetClipboardHistory")
            .WithSummary("Get clipboard history")
            .WithDescription("Retrieves the history of clipboard operations for the session")
            .Produces<ClipboardHistoryResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Get clipboard statistics
        clipboardGroup.MapGet("/stats", GetClipboardStatsAsync)
            .WithName("GetClipboardStats")
            .WithSummary("Get clipboard statistics")
            .WithDescription("Retrieves statistics about clipboard usage for the session")
            .Produces<ClipboardStatistics>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Copies content to the shared clipboard.
    /// </summary>
    private static async Task<IResult> CopyToClipboardAsync(
        [FromRoute] Guid sessionId,
        [FromBody] CopyToClipboardRequest request,
        [FromServices] IClipboardManager clipboardManager,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            if (request.SessionId != sessionId)
            {
                return Results.Problem(
                    title: "Session ID Mismatch",
                    detail: "The session ID in the URL does not match the session ID in the request body.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            logger.LogDebug("Copying content to clipboard for session {SessionId} from device {DeviceId}", 
                sessionId, request.DeviceId);

            // Verify session exists
            try
            {
                var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
                logger.LogDebug("Session {SessionId} verified successfully", sessionId);
            }
            catch (SessionNotFoundException)
            {
                logger.LogWarning("Session {SessionId} not found for clipboard copy", sessionId);
                return Results.Problem(
                    title: "Session Not Found",
                    detail: $"Session with ID {sessionId} was not found or has expired.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            // Copy to clipboard
            var result = await clipboardManager.CopyToClipboardAsync(request);

            if (!result.Success)
            {
                logger.LogWarning("Failed to copy to clipboard for session {SessionId}: {ErrorMessage}", 
                    sessionId, result.ErrorMessage);
                
                return Results.Problem(
                    title: "Copy Failed",
                    detail: result.ErrorMessage ?? "Failed to copy content to clipboard.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            logger.LogInformation("Successfully copied content to clipboard for session {SessionId}", sessionId);
            return Results.Ok(result);
        }
        catch (ClipboardValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed for clipboard copy in session {SessionId}", sessionId);
            return Results.Problem(
                title: "Validation Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for clipboard copy", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy to clipboard for session {SessionId}", sessionId);
            return Results.Problem(
                title: "Copy Failed",
                detail: "An error occurred while copying content to the clipboard.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Gets the current clipboard content.
    /// </summary>
    private static async Task<IResult> GetClipboardAsync(
        [FromRoute] Guid sessionId,
        [FromQuery] Guid? deviceId,
        [FromServices] IClipboardManager clipboardManager,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("Getting clipboard content for session {SessionId}", sessionId);

            // Verify session exists
            try
            {
                var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
                logger.LogDebug("Session {SessionId} verified successfully", sessionId);
            }
            catch (SessionNotFoundException)
            {
                logger.LogWarning("Session {SessionId} not found for clipboard get", sessionId);
                return Results.Problem(
                    title: "Session Not Found",
                    detail: $"Session with ID {sessionId} was not found or has expired.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            // Use provided deviceId or generate a temporary one
            var requestDeviceId = deviceId ?? Guid.NewGuid();
            var request = new GetClipboardRequest(sessionId, requestDeviceId);
            var result = await clipboardManager.GetClipboardAsync(request);

            return Results.Ok(result);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for clipboard get", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get clipboard content for session {SessionId}", sessionId);
            return Results.Problem(
                title: "Get Clipboard Failed",
                detail: "An error occurred while retrieving clipboard content.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Clears the clipboard content.
    /// </summary>
    private static async Task<IResult> ClearClipboardAsync(
        [FromRoute] Guid sessionId,
        [FromServices] IClipboardManager clipboardManager,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger,
        [FromQuery] string? deviceId = null)
    {
        try
        {
            logger.LogDebug("Clearing clipboard content for session {SessionId}", sessionId);

            // Verify session exists
            try
            {
                var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
                logger.LogDebug("Session {SessionId} verified successfully", sessionId);
            }
            catch (SessionNotFoundException)
            {
                logger.LogWarning("Session {SessionId} not found for clipboard clear", sessionId);
                return Results.Problem(
                    title: "Session Not Found",
                    detail: $"Session with ID {sessionId} was not found or has expired.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            // Parse device ID or use a default one
            var requestDeviceId = Guid.NewGuid();
            if (!string.IsNullOrEmpty(deviceId) && !Guid.TryParse(deviceId, out requestDeviceId))
            {
                return Results.Problem(
                    title: "Invalid Device ID",
                    detail: "Device ID must be a valid GUID.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var result = await clipboardManager.ClearClipboardAsync(sessionId, requestDeviceId);

            if (!result)
            {
                logger.LogWarning("Failed to clear clipboard for session {SessionId}", sessionId);
                
                return Results.Problem(
                    title: "Clear Failed",
                    detail: "Failed to clear clipboard content.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            logger.LogInformation("Successfully cleared clipboard for session {SessionId}", sessionId);
            return Results.Ok(new MessageResponse("Clipboard cleared successfully", true));
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for clipboard clear", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear clipboard for session {SessionId}", sessionId);
            return Results.Problem(
                title: "Clear Failed",
                detail: "An error occurred while clearing the clipboard.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Gets the clipboard history.
    /// </summary>
    private static async Task<IResult> GetClipboardHistoryAsync(
        [FromRoute] Guid sessionId,
        [FromServices] IClipboardManager clipboardManager,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger,
        [FromQuery] int limit = 10,
        [FromQuery] Guid? deviceId = null)
    {
        try
        {
            logger.LogDebug("Getting clipboard history for session {SessionId}", sessionId);

            // Verify session exists
            try
            {
                var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
                logger.LogDebug("Session {SessionId} verified successfully", sessionId);
            }
            catch (SessionNotFoundException)
            {
                logger.LogWarning("Session {SessionId} not found for clipboard history", sessionId);
                return Results.Problem(
                    title: "Session Not Found",
                    detail: $"Session with ID {sessionId} was not found or has expired.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            // Use provided deviceId or generate a temporary one
            var requestDeviceId = deviceId ?? Guid.NewGuid();
            var boundedLimit = Math.Max(1, Math.Min(limit, 100));
            
            var result = await clipboardManager.GetClipboardHistoryAsync(sessionId, requestDeviceId, boundedLimit);

            var response = new ClipboardHistoryResponse(result, true);
            return Results.Ok(response);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for clipboard history", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get clipboard history for session {SessionId}", sessionId);
            return Results.Problem(
                title: "Get History Failed",
                detail: "An error occurred while retrieving clipboard history.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Gets clipboard statistics.
    /// </summary>
    private static async Task<IResult> GetClipboardStatsAsync(
        [FromRoute] Guid sessionId,
        [FromServices] IClipboardManager clipboardManager,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("Getting clipboard statistics for session {SessionId}", sessionId);

            // Verify session exists
            try
            {
                var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
                logger.LogDebug("Session {SessionId} verified successfully", sessionId);
            }
            catch (SessionNotFoundException)
            {
                logger.LogWarning("Session {SessionId} not found for clipboard stats", sessionId);
                return Results.Problem(
                    title: "Session Not Found",
                    detail: $"Session with ID {sessionId} was not found or has expired.",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var result = await clipboardManager.GetClipboardStatisticsAsync(sessionId);

            return Results.Ok(result);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for clipboard stats", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get clipboard statistics for session {SessionId}", sessionId);
            return Results.Problem(
                title: "Get Statistics Failed",
                detail: "An error occurred while retrieving clipboard statistics.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
