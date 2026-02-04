using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// User device management endpoints.
/// Issue #3340: Login device tracking and management.
/// </summary>
internal static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        // GET /users/me/devices - List user's devices
        group.MapGet("/users/me/devices", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetUserDevicesQuery(
                UserId: session!.User!.Id,
                CurrentSessionId: session.SessionId
            );

            var devices = await mediator.Send(query, ct).ConfigureAwait(false);
            logger.LogInformation("User {UserId} retrieved {Count} devices", session.User.Id, devices.Count);

            return Results.Json(devices);
        })
        .Produces<List<UserDeviceDto>>(200)
        .Produces(401)
        .WithTags("Devices", "User")
        .WithSummary("Get user's devices")
        .WithDescription("Retrieves all active devices (sessions) for the current user with parsed device information. " +
                        "The current device is marked with IsCurrentDevice=true.");

        // DELETE /users/me/devices/{deviceId} - Revoke a specific device
        group.MapDelete("/users/me/devices/{deviceId:guid}", async (
            Guid deviceId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Check if trying to revoke current device
            if (session!.SessionId == deviceId)
            {
                return Results.BadRequest(new
                {
                    error = "Cannot revoke current device",
                    message = "Use the logout endpoint to sign out of the current device"
                });
            }

            var command = new RevokeSessionCommand(
                SessionId: deviceId,
                RequestingUserId: session.User!.Id,
                IsRequestingUserAdmin: false,
                Reason: "User revoked device"
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("User {UserId} failed to revoke device {DeviceId}: {Error}",
                    session.User.Id, deviceId, result.ErrorMessage);
                return Results.NotFound(new { error = result.ErrorMessage ?? "Device not found or already revoked" });
            }

            logger.LogInformation("User {UserId} revoked device {DeviceId}", session.User.Id, deviceId);
            return Results.Json(new { ok = true, message = "Device revoked successfully" });
        })
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Devices", "User")
        .WithSummary("Revoke a device")
        .WithDescription("Revokes access from a specific device by invalidating its session. " +
                        "Cannot revoke the current device - use logout endpoint instead.");

        return group;
    }
}
