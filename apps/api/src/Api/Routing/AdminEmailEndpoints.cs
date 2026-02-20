using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for email queue monitoring and management.
/// Issue #4430: Email queue dashboard monitoring.
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
}
