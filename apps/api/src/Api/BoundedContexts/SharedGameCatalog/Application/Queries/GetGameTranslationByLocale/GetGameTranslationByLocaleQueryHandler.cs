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
/// <see cref="Locale.TryCreate"/> so caller variants ("EN", "en-gb", " it ")
/// all hit the same canonical row. Locale validation runs upstream via
/// <see cref="GetGameTranslationByLocaleQueryValidator"/> (FluentValidation →
/// HTTP 422 with "Invalid ISO 639-1 locale"). Issue #2399: handler trusts the
/// validator and degrades gracefully via <c>TryCreate</c> if a direct
/// <c>IMediator.Send</c> bypasses <c>ValidationBehavior</c> (returns null
/// instead of throwing — same shape as a missing translation row).
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

        // Issue #2399: validator already rejected invalid locales with 422.
        // TryCreate keeps the handler exception-free even if ValidationBehavior
        // is bypassed (defense-in-depth) — returns null, same shape as the
        // "no row matches" path below, so the endpoint maps it to HTTP 404.
        if (!Locale.TryCreate(query.Locale, out var locale))
        {
            return null;
        }

        var translation = await _translationRepo
            .GetByGameIdAndLocaleAsync(query.GameId, locale.Value, cancellationToken)
            .ConfigureAwait(false);

        return translation is null
            ? null
            : SharedGameTranslationProjections.ToDetailDto(translation);
    }
}
