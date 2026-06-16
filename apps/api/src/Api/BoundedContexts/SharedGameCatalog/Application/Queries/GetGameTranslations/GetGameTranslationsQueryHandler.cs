using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslations;

/// <summary>
/// Handler for <see cref="GetGameTranslationsQuery"/>. Returns the full list of
/// active translations for the requested game (soft-deleted rows are filtered
/// out by the repository's query filter). Issue #2339 — sub-PR 1/3 Wave 4.
/// </summary>
/// <remarks>
/// Read-side, no domain mutation — pure projection through
/// <see cref="SharedGameTranslationProjections.ToDetailDto"/>. Endpoint integration
/// tests in Wave 5 (Task 14) cover the happy-path + empty-list cases; no
/// dedicated unit tests for this trivial handler per plan Task 12.
/// </remarks>
internal sealed class GetGameTranslationsQueryHandler
    : IRequestHandler<GetGameTranslationsQuery, IReadOnlyList<SharedGameTranslationDetailDto>>
{
    private readonly ISharedGameTranslationRepository _translationRepo;

    public GetGameTranslationsQueryHandler(ISharedGameTranslationRepository translationRepo)
    {
        ArgumentNullException.ThrowIfNull(translationRepo);
        _translationRepo = translationRepo;
    }

    public async Task<IReadOnlyList<SharedGameTranslationDetailDto>> Handle(
        GetGameTranslationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var translations = await _translationRepo
            .GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        return translations.Select(SharedGameTranslationProjections.ToDetailDto).ToList();
    }
}
