using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetGamebookCampaignHandler : IRequestHandler<GetGamebookCampaignQuery, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly ISessionBookProgressRepository _progress;

    public GetGamebookCampaignHandler(
        IGamebookCampaignSessionRepository campaigns,
        ISessionBookProgressRepository progress)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(progress);
        _campaigns = campaigns;
        _progress = progress;
    }

    public async Task<GamebookCampaignDto> Handle(GetGamebookCampaignQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _campaigns.GetByIdAsync(request.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {request.CampaignId} not found");

        if (session.OwnerUserId != request.CallerUserId)
            throw new ConflictException("Forbidden");

        // Load the most-recently-visited per-book progress row so resume returns the
        // user's last reading position (regression fix for PR #1362 code review).
        var progress = await _progress
            .GetMostRecentByCampaignAsync(session.Id, cancellationToken)
            .ConfigureAwait(false);

        return CreateGamebookCampaignHandler.MapToDto(session, progress);
    }
}
