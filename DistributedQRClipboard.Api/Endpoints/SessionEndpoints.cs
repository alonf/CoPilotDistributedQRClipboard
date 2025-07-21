using Microsoft.AspNetCore.Mvc;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Models;
using DistributedQRClipboard.Core.Exceptions;

namespace DistributedQRClipboard.Api.Endpoints;

/// <summary>
/// Session management endpoints for creating and joining sessions.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>
    /// Configures session-related endpoints.
    /// </summary>
    /// <param name="app">The web application</param>
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var sessionGroup = app.MapGroup("/api/sessions")
            .WithTags("Sessions")
            .WithOpenApi();

        // Create a new session
        sessionGroup.MapPost("/", CreateSessionAsync)
            .WithName("CreateSession")
            .WithSummary("Create a new session")
            .WithDescription("Creates a new session and returns session information with QR code")
            .Produces<CreateSessionResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Get session information
        sessionGroup.MapGet("/{sessionId:guid}", GetSessionAsync)
            .WithName("GetSession")
            .WithSummary("Get session information")
            .WithDescription("Retrieves information about an existing session")
            .Produces<SessionInfo>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Get session QR code
        sessionGroup.MapGet("/{sessionId:guid}/qr-code", GetSessionQrCodeAsync)
            .WithName("GetSessionQrCode")
            .WithSummary("Get QR code for session")
            .WithDescription("Generates and returns a QR code for the specified session")
            .Produces<string>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Join an existing session
        sessionGroup.MapPost("/{sessionId:guid}/join", JoinSessionAsync)
            .WithName("JoinSession")
            .WithSummary("Join an existing session")
            .WithDescription("Joins a device to an existing session")
            .Produces<JoinSessionResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // Leave a session
        sessionGroup.MapDelete("/{sessionId:guid}/leave", LeaveSessionAsync)
            .WithName("LeaveSession")
            .WithSummary("Leave a session")
            .WithDescription("Removes a device from a session")
            .Produces<MessageResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Creates a new session.
    /// </summary>
    private static async Task<IResult> CreateSessionAsync(
        [FromServices] ISessionManager sessionManager,
        [FromServices] IQrCodeGenerator qrCodeGenerator,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("Creating new session");

            // Create session with default expiration
            var createRequest = new CreateSessionRequest(DeviceName: "API Device", ExpirationMinutes: 1440); // 24 hours
            var createResult = await sessionManager.CreateSessionAsync(createRequest);

            if (!createResult.Success || createResult.SessionInfo == null)
            {
                logger.LogError("Failed to create session: {ErrorMessage}", createResult.ErrorMessage);
                return Results.Problem(
                    title: "Session Creation Failed",
                    detail: createResult.ErrorMessage ?? "Failed to create session.",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }

            var sessionInfo = createResult.SessionInfo.Value;
            
            // Generate QR code for the session
            var qrCodeBase64 = await qrCodeGenerator.GenerateQrCodeAsync(sessionInfo.SessionId);
            var joinUrl = qrCodeGenerator.GenerateJoinUrl(sessionInfo.SessionId);

            var response = new CreateSessionResponse(
                sessionInfo,
                joinUrl,
                qrCodeBase64,
                true
            );

            logger.LogInformation("Successfully created session {SessionId}", sessionInfo.SessionId);
            return Results.Created($"/api/sessions/{sessionInfo.SessionId}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create session");
            return Results.Problem(
                title: "Session Creation Failed",
                detail: "An error occurred while creating the session. Please try again.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Gets information about a session.
    /// </summary>
    private static async Task<IResult> GetSessionAsync(
        [FromRoute] Guid sessionId,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("Getting session information for {SessionId}", sessionId);

            var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
            return Results.Ok(sessionInfo);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
            return Results.Problem(
                title: "Session Retrieval Failed",
                detail: "An error occurred while retrieving the session information.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Gets QR code for a session.
    /// </summary>
    private static async Task<IResult> GetSessionQrCodeAsync(
        [FromRoute] Guid sessionId,
        [FromServices] ISessionManager sessionManager,
        [FromServices] IQrCodeGenerator qrCodeGenerator,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogDebug("Generating QR code for session {SessionId}", sessionId);

            // Verify session exists
            var sessionInfo = await sessionManager.GetSessionAsync(sessionId);
            
            // Generate QR code
            var qrCodeBase64 = await qrCodeGenerator.GenerateQrCodeAsync(sessionId);
            
            return Results.Ok(qrCodeBase64);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for QR code generation", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate QR code for session {SessionId}", sessionId);
            return Results.Problem(
                title: "QR Code Generation Failed",
                detail: "An error occurred while generating the QR code.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Joins a device to a session.
    /// </summary>
    private static async Task<IResult> JoinSessionAsync(
        [FromRoute] Guid sessionId,
        [FromBody] JoinSessionRequest request,
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

            logger.LogDebug("Device {DeviceName} joining session {SessionId}", request.DeviceName, sessionId);

            var result = await sessionManager.JoinSessionAsync(request);

            if (!result.Success)
            {
                logger.LogWarning("Failed to join session {SessionId}: {ErrorMessage}", sessionId, result.ErrorMessage);
                
                var statusCode = result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

                return Results.Problem(
                    title: "Join Session Failed",
                    detail: result.ErrorMessage ?? "Failed to join the session.",
                    statusCode: statusCode
                );
            }

            logger.LogInformation("Device {DeviceName} successfully joined session {SessionId}", request.DeviceName, sessionId);
            return Results.Ok(result);
        }
        catch (SessionNotFoundException)
        {
            logger.LogWarning("Session {SessionId} not found for join request", sessionId);
            return Results.Problem(
                title: "Session Not Found",
                detail: $"Session with ID {sessionId} was not found or has expired.",
                statusCode: StatusCodes.Status404NotFound
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to join session {SessionId}", sessionId);
            return Results.Problem(
                title: "Join Session Failed",
                detail: "An error occurred while joining the session.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Removes a device from a session.
    /// </summary>
    private static async Task<IResult> LeaveSessionAsync(
        [FromRoute] Guid sessionId,
        [FromQuery] string deviceId,
        [FromServices] ISessionManager sessionManager,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Results.Problem(
                    title: "Device ID Required",
                    detail: "Device ID must be provided as a query parameter.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (!Guid.TryParse(deviceId, out var deviceGuid))
            {
                return Results.Problem(
                    title: "Invalid Device ID",
                    detail: "Device ID must be a valid GUID.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            logger.LogDebug("Device {DeviceId} leaving session {SessionId}", deviceId, sessionId);

            var result = await sessionManager.LeaveSessionAsync(sessionId, deviceGuid, DeviceLeaveReason.Disconnect);

            logger.LogInformation("Device {DeviceId} successfully left session {SessionId}", deviceId, sessionId);
            return Results.Ok(new MessageResponse("Successfully left the session", true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to leave session {SessionId} for device {DeviceId}", sessionId, deviceId);
            return Results.Problem(
                title: "Leave Session Failed",
                detail: "An error occurred while leaving the session.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}

/// <summary>
/// Generic message response.
/// </summary>
/// <param name="Message">The response message</param>
/// <param name="Success">Whether the operation was successful</param>
public sealed record MessageResponse(string Message, bool Success);
