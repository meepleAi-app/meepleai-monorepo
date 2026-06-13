using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Issue #1823 Wave 3 M13 — paginated list of Wikidata cover enrichment
/// dead-letter attempts for the admin visibility page. Filterable by
/// terminal reason (e.g. <c>r2-upload-error</c>, <c>license-not-whitelisted</c>).
/// </summary>
/// <param name="Skip">Pagination offset (clamped to 0).</param>
/// <param name="Take">Page size (clamped to [1, 200]).</param>
/// <param name="ReasonFilter">Optional exact match on the <c>Reason</c> column; null returns all dead-letters.</param>
internal sealed record GetWikidataDeadLetterAttemptsQuery(
    int Skip,
    int Take,
    string? ReasonFilter) : IRequest<WikidataDeadLetterAttemptsResult>;

/// <summary>
/// Flat DTO returned by <see cref="GetWikidataDeadLetterAttemptsQuery"/>.
/// </summary>
public sealed record WikidataDeadLetterAttemptsResult(
    IReadOnlyList<WikidataDeadLetterAttemptDto> Items,
    int TotalCount,
    int Skip,
    int Take);

/// <summary>
/// Per-row dead-letter DTO surfaced on the M13 admin page.
/// </summary>
public sealed record WikidataDeadLetterAttemptDto(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    DateTime AttemptedAt,
    DateTime DeadLetteredAt,
    string Reason,
    string? Details,
    int RetryCount);

internal sealed class GetWikidataDeadLetterAttemptsQueryHandler
    : IRequestHandler<GetWikidataDeadLetterAttemptsQuery, WikidataDeadLetterAttemptsResult>
{
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;

    public GetWikidataDeadLetterAttemptsQueryHandler(IWikidataCoverEnrichmentAttemptRepository attempts)
    {
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
    }

    public async Task<WikidataDeadLetterAttemptsResult> Handle(
        GetWikidataDeadLetterAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 200);

        // Task 2.3 bridge — Task 3.3 will propagate an optional
        // IncludeAcknowledged param through the query DTO. For now retain the
        // existing semantics (hide acknowledged rows on the default open-work
        // view) by hard-coding includeAcknowledged: false.
        var page = await _attempts
            .GetDeadLettersAsync(skip, take, request.ReasonFilter, includeAcknowledged: false, cancellationToken)
            .ConfigureAwait(false);

        var items = page.Items
            .Select(row => new WikidataDeadLetterAttemptDto(
                row.Id,
                row.SharedGameId,
                row.GameTitle,
                row.AttemptedAt,
                row.DeadLetteredAt,
                row.Reason,
                row.Details,
                row.RetryCount))
            .ToList();

        return new WikidataDeadLetterAttemptsResult(items, page.TotalCount, skip, take);
    }
}
