using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Chat history tier limits configuration endpoints (Admin only).
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
internal static class ChatHistoryConfigEndpoints
{
    public static RouteGroupBuilder MapChatHistoryConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/config/chat-history-limits", HandleGetChatHistoryLimits)
            .WithName("GetChatHistoryLimits")
            .WithTags("Admin", "ChatHistory", "Configuration")
            .WithSummary("Get current chat history tier limits")
            .WithDescription("Retrieves the maximum chat session counts for Free, Normal, and Premium tiers")
            .Produces<ChatHistoryLimitsDto>();

        group.MapPut("/admin/config/chat-history-limits", HandleUpdateChatHistoryLimits)
            .WithName("UpdateChatHistoryLimits")
            .WithTags("Admin", "ChatHistory", "Configuration")
            .WithSummary("Update chat history tier limits")
            .WithDescription("Updates the maximum chat session counts for all tiers. Requires admin role.")
            .Produces<ChatHistoryLimitsDto>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<IResult> HandleGetChatHistoryLimits(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetChatHistoryLimitsQuery();
        var limits = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(limits);
    }

    private static async Task<IResult> HandleUpdateChatHistoryLimits(
        UpdateChatHistoryLimitsRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {UserId} updating chat history limits: Free={Free}, Normal={Normal}, Premium={Premium}",
            userId,
            request.FreeTierLimit,
            request.NormalTierLimit,
            request.PremiumTierLimit);

        var command = new UpdateChatHistoryLimitsCommand(
            FreeTierLimit: request.FreeTierLimit,
            NormalTierLimit: request.NormalTierLimit,
            PremiumTierLimit: request.PremiumTierLimit,
            UpdatedByUserId: userId
        );

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Admin {UserId} successfully updated chat history limits",
            userId);

        return Results.Ok(result);
    }
}
