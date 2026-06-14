using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Issue #1823 Wave 3 M13 — paginated list of Wikidata cover enrichment
/// dead-letter attempts for the admin visibility page. Filterable by
/// terminal reason (e.g. <c>r2-upload-error</c>, <c>license-not-whitelisted</c>).
/// Phase F (#2254) added <see cref="IncludeAcknowledged"/> so the admin UI can
/// toggle between the default open-work view (hide acknowledged rows) and the
/// historical audit view (show everything).
/// </summary>
/// <param name="Skip">Pagination offset (clamped to 0).</param>
/// <param name="Take">Page size (clamped to [1, 200]).</param>
/// <param name="ReasonFilter">Optional exact match on the <c>Reason</c> column; null returns all dead-letters.</param>
/// <param name="IncludeAcknowledged">
/// When <see langword="false"/> (default), rows whose <c>AcknowledgedAt</c> is
/// non-null are hidden so operators only see open-work items. When
/// <see langword="true"/>, all dead-letters are returned for the audit view.
/// </param>
internal sealed record GetWikidataDeadLetterAttemptsQuery(
    int Skip,
    int Take,
    string? ReasonFilter,
    bool IncludeAcknowledged = false) : IRequest<WikidataDeadLetterAttemptsResult>;

/// <summary>
/// Flat DTO returned by <see cref="GetWikidataDeadLetterAttemptsQuery"/>.
/// </summary>
public sealed record WikidataDeadLetterAttemptsResult(
    IReadOnlyList<WikidataDeadLetterAttemptDto> Items,
    int TotalCount,
    int Skip,
    int Take);

/// <summary>
/// Per-row dead-letter DTO surfaced on the M13 admin page. Phase F extended
/// the shape with F5 ack metadata and F6 admin attribution surfaced via the
/// repository LEFT JOIN on Users.
/// </summary>
/// <param name="Id">Attempt id.</param>
/// <param name="SharedGameId">Parent shared game id.</param>
/// <param name="GameTitle">Denormalized parent title (fallback "(deleted game)").</param>
/// <param name="AttemptedAt">UTC attempt timestamp.</param>
/// <param name="DeadLetteredAt">UTC dead-letter timestamp.</param>
/// <param name="Reason">Stable machine-readable reason.</param>
/// <param name="Details">Optional human-readable detail.</param>
/// <param name="RetryCount">0..N counter from the original pipeline.</param>
/// <param name="AcknowledgedAt">F5 — UTC ack timestamp (null if not acknowledged).</param>
/// <param name="AcknowledgedBy">F5 — user id who acknowledged (null if not acknowledged).</param>
/// <param name="AcknowledgedByFullName">F5 — display name of the acknowledging user (null if not acknowledged or user deleted).</param>
/// <param name="TriggeredByAdminUserId">F6 — admin user id that triggered this attempt via M12/F2 (null for M9 scheduler).</param>
/// <param name="TriggeredByAdminFullName">F6 — display name of the triggering admin (null when M9 scheduler or user deleted).</param>
public sealed record WikidataDeadLetterAttemptDto(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    DateTime AttemptedAt,
    DateTime DeadLetteredAt,
    string Reason,
    string? Details,
    int RetryCount,
    DateTime? AcknowledgedAt,
    Guid? AcknowledgedBy,
    string? AcknowledgedByFullName,
    Guid? TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);

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

        var page = await _attempts
            .GetDeadLettersAsync(skip, take, request.ReasonFilter, request.IncludeAcknowledged, cancellationToken)
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
                row.RetryCount,
                row.AcknowledgedAt,
                row.AcknowledgedBy,
                row.AcknowledgedByFullName,
                row.TriggeredByAdminUserId,
                row.TriggeredByAdminFullName))
            .ToList();

        return new WikidataDeadLetterAttemptsResult(items, page.TotalCount, skip, take);
    }
}
