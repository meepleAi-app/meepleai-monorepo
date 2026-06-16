using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;

/// <summary>
/// Handler for <see cref="GetGameTranslationByLocaleQuery"/>. Returns the
/// matching active translation or <c>null</c> when missing (DEC-M2 — endpoint
/// maps null to HTTP 404). Issue #2339 — sub-PR 1/3 Wave 4 (Task 12).
/// </summary>
/// <remarks>
/// Read-side, no domain mutation. The raw locale string is normalised through
/// <see cref="Locale.Create"/> so caller variants ("EN", "en-gb", " it ") all
/// hit the same canonical row. Locale validation runs upstream via
/// <see cref="GetGameTranslationByLocaleQueryValidator"/> (FluentValidation →
/// HTTP 422 with "Invalid ISO 639-1 locale"); the <c>Locale.Create</c> call
/// here is defense-in-depth for direct <c>IMediator.Send</c> paths that may
/// bypass <c>ValidationBehavior</c>.
/// </remarks>
internal sealed class GetGameTranslationByLocaleQueryHandler
    : IRequestHandler<GetGameTranslationByLocaleQuery, SharedGameTranslationDetailDto?>
{
    private readonly ISharedGameTranslationRepository _translationRepo;

    public GetGameTranslationByLocaleQueryHandler(ISharedGameTranslationRepository translationRepo)
    {
        ArgumentNullException.ThrowIfNull(translationRepo);
        _translationRepo = translationRepo;
    }

    public async Task<SharedGameTranslationDetailDto?> Handle(
        GetGameTranslationByLocaleQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var locale = Locale.Create(query.Locale);

        var translation = await _translationRepo
            .GetByGameIdAndLocaleAsync(query.GameId, locale.Value, cancellationToken)
            .ConfigureAwait(false);

        return translation is null
            ? null
            : SharedGameTranslationProjections.ToDetailDto(translation);
    }
}
