using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetGamebookCampaignHandler : IRequestHandler<GetGamebookCampaignQuery, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public GetGamebookCampaignHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GamebookCampaignDto> Handle(GetGamebookCampaignQuery q, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(q.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {q.CampaignId} not found");

        if (session.OwnerUserId != q.CallerUserId)
            throw new ConflictException("Forbidden");

        return CreateGamebookCampaignHandler.MapToDto(session, progress: null);
    }
}
