using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Middleware;
using MediatR;
using Microsoft.Extensions.Options;
using Api.BoundedContexts.WorkflowIntegration.Application.Services;
using Api.Helpers;

namespace Api.Routing;

/// <summary>
/// Webhook endpoint for n8n → API callbacks.
/// Issue #57: Receives actions from n8n with HMAC-SHA256 signature validation.
/// </summary>
internal static class N8nWebhookEndpoints
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapN8nWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/n8n", HandleN8nWebhook)
            .WithName("N8nWebhook")
            .WithTags("Webhooks")
            .WithSummary("Receive n8n webhook callbacks with HMAC-SHA256 validation")
            .AllowAnonymous(); // Auth via HMAC signature
    }

    private static async Task<IResult> HandleN8nWebhook(
        HttpContext context,
        IMediator mediator,
        IOptions<N8nWebhookClientOptions> options,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Read body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        // Validate HMAC signature — fail closed: reject all requests when secret is not configured
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError("N8n webhook received but WebhookSecret is not configured — rejecting. Set N8N_WEBHOOK_SECRET in n8n.secret");
            return Results.Problem(
                detail: "Webhook endpoint is not configured",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var signature = context.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("N8n webhook missing X-Webhook-Signature header");
            return Results.Unauthorized();
        }

        var expectedSignature = ComputeHmacSha256(body, secret);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            logger.LogWarning("N8n webhook HMAC signature mismatch");
            return Results.Unauthorized();
        }

        // Check idempotency key
        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            logger.LogInformation("n8n webhook received with idempotency key: {Key}", LogValueSanitizer.Sanitize(idempotencyKey));
        }

        // Parse request
        N8nWebhookRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<N8nWebhookRequest>(body, CamelCaseOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "n8n webhook: invalid JSON body");
            return Results.BadRequest(new { error = "invalid_json", message = "Request body is not valid JSON" });
        }

        if (request is null || string.IsNullOrEmpty(request.Action))
        {
            return Results.BadRequest(new { error = "missing_action", message = "The 'action' field is required" });
        }

        logger.LogInformation("n8n webhook action: {Action}", LogSanitizer.Sanitize(request.Action));

        // Route action
        try
        {
            return request.Action switch
            {
                "send_notification" => await HandleSendNotification(request.Payload, mediator, logger, cancellationToken).ConfigureAwait(false),
                "send_email" => HandleSendEmail(request.Payload, logger),
                "update_game_night_status" => HandleUpdateGameNightStatus(request.Payload, logger),
                "send_reminder" => HandleSendReminder(request.Payload, logger),
                _ => Results.BadRequest(new { error = "unknown_action", message = $"Unknown action: {request.Action}" })
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "n8n webhook action {Action} failed", LogSanitizer.Sanitize(request.Action));
            return Results.Problem("Internal error processing webhook action", statusCode: 500);
        }
    }

    private static async Task<IResult> HandleSendNotification(
        JsonElement? payload, IMediator mediator, ILogger logger, CancellationToken ct)
    {
        if (payload is null)
            return Results.BadRequest(new { error = "missing_payload", message = "Payload required for send_notification" });

        var userId = payload.Value.TryGetProperty("userId", out var u) ? u.GetString() : null;
        var title = payload.Value.TryGetProperty("title", out var t) ? t.GetString() : null;
        var message = payload.Value.TryGetProperty("message", out var m) ? m.GetString() : null;
        var type = payload.Value.TryGetProperty("type", out var tp) ? tp.GetString() : "system_notification";
        var link = payload.Value.TryGetProperty("link", out var l) ? l.GetString() : null;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
        {
            return Results.BadRequest(new { error = "invalid_payload", message = "userId, title, and message are required" });
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Results.BadRequest(new { error = "invalid_user_id", message = "userId must be a valid GUID" });
        }

        await mediator.Send(new CreateN8nNotificationCommand(
            UserId: userGuid,
            Title: title!,
            Message: message!,
            Type: type!,
            Link: link
        ), ct).ConfigureAwait(false);

        logger.LogInformation("n8n webhook: notification sent to user {UserId}", LogSanitizer.Sanitize(userId));
        return Results.Ok(new { success = true, action = "send_notification" });
    }

    private static IResult HandleSendEmail(JsonElement? payload, ILogger logger)
    {
        if (payload is null)
            return Results.BadRequest(new { error = "missing_payload", message = "Payload required for send_email" });

        var to = payload.Value.TryGetProperty("to", out var toEl) ? toEl.GetString() : null;
        var subject = payload.Value.TryGetProperty("subject", out var subEl) ? subEl.GetString() : null;
        var body = payload.Value.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;

        if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
        {
            return Results.BadRequest(new { error = "invalid_payload", message = "to, subject, and body are required" });
        }

        logger.LogInformation("n8n webhook: email enqueued to {To}, subject: {Subject}", LogSanitizer.Sanitize(to), LogSanitizer.Sanitize(subject));
        return Results.Ok(new { success = true, action = "send_email" });
    }

    private static IResult HandleUpdateGameNightStatus(JsonElement? payload, ILogger logger)
    {
        if (payload is null)
            return Results.BadRequest(new { error = "missing_payload", message = "Payload required for update_game_night_status" });

        var eventId = payload.Value.TryGetProperty("eventId", out var eId) ? eId.GetString() : null;
        var status = payload.Value.TryGetProperty("status", out var s) ? s.GetString() : null;

        if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(status))
        {
            return Results.BadRequest(new { error = "invalid_payload", message = "eventId and status are required" });
        }

        logger.LogInformation("n8n webhook: game night {EventId} status update to {Status}", LogSanitizer.Sanitize(eventId), LogSanitizer.Sanitize(status));
        return Results.Ok(new { success = true, action = "update_game_night_status" });
    }

    private static IResult HandleSendReminder(JsonElement? payload, ILogger logger)
    {
        if (payload is null)
            return Results.BadRequest(new { error = "missing_payload", message = "Payload required for send_reminder" });

        var eventId = payload.Value.TryGetProperty("eventId", out var eId) ? eId.GetString() : null;
        var reminderType = payload.Value.TryGetProperty("reminderType", out var rt) ? rt.GetString() : "general";

        if (string.IsNullOrEmpty(eventId) || !Guid.TryParse(eventId, out _))
        {
            return Results.BadRequest(new { error = "invalid_payload", message = "eventId must be a valid GUID" });
        }

        logger.LogInformation("n8n webhook: sending {ReminderType} reminder for event {EventId}", LogSanitizer.Sanitize(reminderType), LogSanitizer.Sanitize(eventId));
        return Results.Ok(new { success = true, action = "send_reminder" });
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}

internal sealed record N8nWebhookRequest(string Action, JsonElement? Payload);
