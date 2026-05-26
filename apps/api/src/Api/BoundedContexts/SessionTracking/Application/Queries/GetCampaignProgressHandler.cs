using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1388: returns one <see cref="SessionBookProgressDto"/> per book the user
/// has engaged with inside the campaign, sorted by most-recent visit first so the
/// FE "Resume Books" list highlights the latest read. Joins <c>SessionBookProgress</c>
/// rows with <c>GameBook</c> entries server-side via a single batch lookup
/// (<see cref="IGameBookRepository.ListByIdsAsync"/>) to avoid N+1. Orphan progress
/// rows (book deleted under the campaign) are filtered out instead of failing the
/// request — a defensive choice to keep the play surface coherent.
/// </summary>
public class GetCampaignProgressHandler : IRequestHandler<GetCampaignProgressQuery, IReadOnlyList<SessionBookProgressDto>>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly ISessionBookProgressRepository _progress;
    private readonly IGameBookRepository _books;

    public GetCampaignProgressHandler(
        IGamebookCampaignSessionRepository campaigns,
        ISessionBookProgressRepository progress,
        IGameBookRepository books)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(books);
        _campaigns = campaigns;
        _progress = progress;
        _books = books;
    }

    public async Task<IReadOnlyList<SessionBookProgressDto>> Handle(GetCampaignProgressQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _campaigns.GetByIdAsync(request.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {request.CampaignId} not found");

        if (session.OwnerUserId != request.CallerUserId)
            throw new ForbiddenException("Forbidden");

        var progressRows = await _progress.ListByCampaignAsync(session.Id, cancellationToken).ConfigureAwait(false);
        if (progressRows.Count == 0)
        {
            return Array.Empty<SessionBookProgressDto>();
        }

        var bookIds = progressRows.Select(p => p.GameBookId).Distinct().ToList();
        var books = await _books.ListByIdsAsync(bookIds, cancellationToken).ConfigureAwait(false);
        var booksById = books.ToDictionary(b => b.Id);

        return progressRows
            .Where(p => booksById.ContainsKey(p.GameBookId))
            .OrderByDescending(p => p.LastVisitedAt)
            .Select(p => new SessionBookProgressDto(
                p.GameBookId,
                booksById[p.GameBookId].DisplayName,
                p.LastLocation,
                p.LastVisitedAt))
            .ToList();
    }
}
