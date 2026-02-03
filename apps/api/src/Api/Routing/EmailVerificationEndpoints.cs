using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

// DDD CQRS imports
using VerifyEmailCommand = Api.BoundedContexts.Authentication.Application.Commands.EmailVerification.VerifyEmailCommand;
using ResendVerificationCommand = Api.BoundedContexts.Authentication.Application.Commands.EmailVerification.ResendVerificationCommand;

namespace Api.Routing;

/// <summary>
/// Email verification endpoints.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal static class EmailVerificationEndpoints
{
    public static RouteGroupBuilder MapEmailVerificationEndpoints(this RouteGroupBuilder group)
    {
        // Verify email with token
        group.MapPost("/auth/email/verify", async (
            [FromBody] VerifyEmailPayload payload,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Token))
            {
                return Results.BadRequest(new { error = "Verification token is required" });
            }

            var command = new VerifyEmailCommand { Token = payload.Token };
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("Email verification failed: {Message}", result.Message);
                return Results.BadRequest(new { error = result.Message });
            }

            logger.LogInformation("Email verified successfully");
            return Results.Json(new { ok = true, message = result.Message });
        })
        .WithName("VerifyEmail")
        .WithTags("Authentication", "Email Verification")
        .WithSummary("Verify email address using token")
        .WithDescription("Verifies a user's email address using the token sent via email. Tokens expire after 24 hours.")
        .Produces(200)
        .Produces(400);

        // Resend verification email
        group.MapPost("/auth/email/resend", async (
            [FromBody] ResendVerificationPayload payload,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return Results.BadRequest(new { error = "Email is required" });
            }

            var command = new ResendVerificationCommand { Email = payload.Email };
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                // Rate limit exceeded
                return Results.Json(new { error = result.Message }, statusCode: 429);
            }

            // Always return success to prevent email enumeration
            return Results.Json(new { ok = true, message = result.Message });
        })
        .WithName("ResendVerificationEmail")
        .WithTags("Authentication", "Email Verification")
        .WithSummary("Resend verification email")
        .WithDescription("Resends a verification email to the specified address. Rate limited to 1 request per minute.")
        .Produces(200)
        .Produces(400)
        .Produces(429);

        return group;
    }
}

/// <summary>
/// Payload for email verification request.
/// </summary>
internal record VerifyEmailPayload
{
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// Payload for resend verification request.
/// </summary>
internal record ResendVerificationPayload
{
    public string Email { get; init; } = string.Empty;
}
