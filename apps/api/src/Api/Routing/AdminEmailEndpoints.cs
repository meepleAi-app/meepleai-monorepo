using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for email queue monitoring and management.
/// Issue #4430: Email queue dashboard monitoring.
/// Issue #39: Admin email management (history, retry-all, test).
/// </summary>
internal static class AdminEmailEndpoints
{
    public static void MapAdminEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/emails")
            .WithTags("Admin - Email Queue")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/stats", GetEmailQueueStats)
            .WithName("GetEmailQueueStats")
            .WithSummary("Get email queue statistics");

        group.MapGet("/dead-letter", GetDeadLetterEmails)
            .WithName("GetDeadLetterEmails")
            .WithSummary("List dead-lettered emails with pagination");

        group.MapPost("/{id:guid}/retry", AdminRetryEmail)
            .WithName("AdminRetryEmail")
            .WithSummary("Admin force-retry a dead-lettered email");

        // Issue #39: Admin email management endpoints
        group.MapGet("/history", GetEmailHistory)
            .WithName("GetEmailHistory")
            .WithSummary("List email history with pagination and search");

        group.MapPost("/retry-all-dead-letters", RetryAllDeadLetters)
            .WithName("RetryAllDeadLetters")
            .WithSummary("Retry all dead-lettered emails");

        group.MapPost("/test", SendTestEmail)
            .WithName("SendTestEmail")
            .WithSummary("Send a test email (immediate, not queued)");
    }

    private static async Task<IResult> GetEmailQueueStats(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEmailQueueStatsQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDeadLetterEmails(
        IMediator mediator,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetDeadLetterEmailsQuery(skip, take), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> AdminRetryEmail(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AdminRetryEmailCommand(id), cancellationToken).ConfigureAwait(false);
        return result ? Results.Ok(new { success = true }) : Results.NotFound();
    }

    private static async Task<IResult> GetEmailHistory(
        IMediator mediator,
        int skip = 0,
        int take = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAdminEmailHistoryQuery(skip, take, search), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> RetryAllDeadLetters(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RetryAllDeadLettersCommand(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { retried = result });
    }

    private static async Task<IResult> SendTestEmail(
        SendTestEmailRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SendTestEmailCommand(request.To), cancellationToken).ConfigureAwait(false);
        return result ? Results.Ok(new { success = true }) : Results.BadRequest(new { error = "Failed to send test email" });
    }
}

internal record SendTestEmailRequest(string To);
