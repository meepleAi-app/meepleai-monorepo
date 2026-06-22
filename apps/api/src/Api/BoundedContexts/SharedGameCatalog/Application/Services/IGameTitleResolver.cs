namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Batch enricher that joins non-EN translations onto <see cref="SharedGameDto"/> list-query
/// results in a single round-trip. Issue #2339 — sub-PR 1/3 Wave 3 (Task 8).
/// </summary>
/// <remarks>
/// <para>
/// Consumed by the four list query handlers (GetAll / GetFiltered / GetPendingApproval /
/// Search) so that FE callers can pick the right localized title without an N+1 hit on
/// the translations table. Wired in sub-PR 2/3 — this Task 8 only ships the service.
/// </para>
/// <para>
/// The resolver is intentionally a thin wrapper around
/// <see cref="Domain.Repositories.ISharedGameTranslationRepository.GetByGameIdsAsync"/>;
/// the repository's <c>HasQueryFilter(t =&gt; !t.IsDeleted)</c> excludes soft-deleted rows
/// automatically.
/// </para>
/// </remarks>
public interface IGameTitleResolver
{
    /// <summary>
    /// Returns a new list of <see cref="SharedGameDto"/> where each element has
    /// <see cref="SharedGameDto.Translations"/> populated with the active (non-soft-deleted)
    /// translations for that game. Empty input short-circuits with no DB roundtrip.
    /// </summary>
    Task<IReadOnlyList<SharedGameDto>> EnrichAsync(
        IReadOnlyList<SharedGameDto> games,
        CancellationToken cancellationToken);
}
