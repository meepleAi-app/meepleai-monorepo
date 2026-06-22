namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Per-book progress row returned by GET /api/v1/gamebook/campaigns/{id}/progress
/// (issue #1388). One entry per <c>SessionBookProgress</c> row tied to the campaign;
/// the FE renders them in a "Resume Books" list so users can pick up any book they
/// have engaged with.
/// </summary>
/// <param name="BookId">The <c>GameBook.Id</c> the progress row tracks.</param>
/// <param name="BookName">The <c>GameBook.DisplayName</c> resolved server-side; the FE renders this as-is (no client-side join).</param>
/// <param name="LastLocation">Last visited paragraph / section label, formatted as stored (e.g. "§289"). The FE does not interpret <c>ParagraphScheme</c>.</param>
/// <param name="LastVisitedAt">UTC ISO-8601 timestamp of the most recent visit to this book within the campaign.</param>
public sealed record SessionBookProgressDto(
    Guid BookId,
    string BookName,
    string LastLocation,
    DateTimeOffset LastVisitedAt);
