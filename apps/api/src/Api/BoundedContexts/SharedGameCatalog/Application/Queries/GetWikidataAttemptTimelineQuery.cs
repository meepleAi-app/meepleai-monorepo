using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Issue #1823 Phase E F3 — full attempt timeline for one shared game, used by
/// the per-row drawer on the admin dead-letter visibility page.
/// </summary>
/// <param name="GameId">Shared game to inspect.</param>
/// <param name="Limit">Max attempts (clamped to [1, 200]).</param>
internal sealed record GetWikidataAttemptTimelineQuery(
    Guid GameId,
    int Limit) : IRequest<WikidataAttemptTimelineResult>;

/// <summary>
/// Result envelope; empty <see cref="Items"/> when the game has never been processed.
/// </summary>
public sealed record WikidataAttemptTimelineResult(
    Guid GameId,
    IReadOnlyList<WikidataAttemptTimelineNode> Items);

/// <summary>
/// Per-node payload on the timeline. Each maps 1:1 to a
/// <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt"/> row.
/// </summary>
public sealed record WikidataAttemptTimelineNode(
    Guid Id,
    DateTime AttemptedAt,
    string Outcome,
    string? Reason,
    string? Details,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime? DeadLetteredAt);

internal sealed class GetWikidataAttemptTimelineQueryHandler
    : IRequestHandler<GetWikidataAttemptTimelineQuery, WikidataAttemptTimelineResult>
{
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;

    public GetWikidataAttemptTimelineQueryHandler(IWikidataCoverEnrichmentAttemptRepository attempts)
    {
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
    }

    public async Task<WikidataAttemptTimelineResult> Handle(
        GetWikidataAttemptTimelineQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = await _attempts
            .GetAttemptsByGameIdAsync(request.GameId, request.Limit, cancellationToken)
            .ConfigureAwait(false);

        // Task 2.3 bridge — Task 3.4 will extend WikidataAttemptTimelineNode
        // with the F6 admin attribution (TriggeredByAdminUserId +
        // TriggeredByAdminFullName) so the FE drawer can render the badge.
        // For now the row exposes the new repo fields but the DTO ignores
        // them to keep the FE contract stable in this commit.
        var items = rows
            .Select(r => new WikidataAttemptTimelineNode(
                r.Id,
                r.AttemptedAt,
                r.Outcome.ToString(),
                r.Reason,
                r.Details,
                r.RetryCount,
                r.NextRetryAt,
                r.DeadLetteredAt))
            .ToList();

        return new WikidataAttemptTimelineResult(request.GameId, items);
    }
}
