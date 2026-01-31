using Api.Models;
using MediatR;

// DDD CQRS imports
using RequestPasswordResetCommand = Api.BoundedContexts.Authentication.Application.Commands.PasswordReset.RequestPasswordResetCommand;
using ValidatePasswordResetTokenQuery = Api.BoundedContexts.Authentication.Application.Queries.PasswordReset.ValidatePasswordResetTokenQuery;
using ResetPasswordCommand = Api.BoundedContexts.Authentication.Application.Commands.PasswordReset.ResetPasswordCommand;
using DddCreateSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.CreateSessionCommand;

namespace Api.Routing;

/// <summary>
/// Password reset endpoints.
/// Handles password reset request, token validation, and password change with auto-login.
/// </summary>
internal static class PasswordEndpoints
{
    // AUTH-04: Password reset endpoints
    public static RouteGroupBuilder MapPasswordResetEndpoints(this RouteGroupBuilder group, Action<HttpContext, string, DateTime> writeSessionCookie)
    {
        group.MapPost("/auth/password-reset/request", async (
            PasswordResetRequestPayload payload,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return Results.BadRequest(new { error = "Email is required" });
            }

            // Execute password reset request via CQRS handler
            var command = new RequestPasswordResetCommand { Email = payload.Email };
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.StatusCode(429); // Too Many Requests
            }

            // Always return success to prevent email enumeration
            return Results.Json(new { ok = true, message = result.Message });
        });

        group.MapGet("/auth/password-reset/verify", async (
            string token,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new { error = "Token is required" });
            }

            // Execute password reset token validation via CQRS query
            var query = new ValidatePasswordResetTokenQuery { Token = token };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (!result.IsValid)
            {
                return Results.BadRequest(new { error = "Invalid or expired token" });
            }

            return Results.Json(new { ok = true, message = "Token is valid" });
        });

        group.MapPut("/auth/password-reset/confirm", async (
            PasswordResetConfirmPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Token))
            {
                return Results.BadRequest(new { error = "Token is required" });
            }

            if (string.IsNullOrWhiteSpace(payload.NewPassword))
            {
                return Results.BadRequest(new { error = "New password is required" });
            }

            // Execute password reset via CQRS command
            var resetCommand = new ResetPasswordCommand
            {
                Token = payload.Token,
                NewPassword = payload.NewPassword
            };

            var resetResult = await mediator.Send(resetCommand, ct).ConfigureAwait(false);

            if (!resetResult.Success || resetResult.UserId == null)
            {
                return Results.NotFound(new { error = resetResult.ErrorMessage ?? "Invalid or expired token" });
            }

            // Create new session for auto-login - DDD CQRS
            var sessionCommand = new DddCreateSessionCommand(
                UserId: resetResult.UserId.Value,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            var sessionResult = await mediator.Send(sessionCommand, ct).ConfigureAwait(false);

            writeSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);

            return Results.Json(new { ok = true, message = "Password has been reset successfully" });
        });

        return group;
    }
}
