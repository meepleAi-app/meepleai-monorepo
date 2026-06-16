using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Read-side projections from <see cref="SharedGameTranslation"/> aggregates to
/// the DTOs exposed by translation endpoints. Issue #2379 (F2) — consolidates
/// the previously separated <c>SharedGameTranslationMapper.ToDetailDto</c> and
/// the private <c>GameTitleResolver.ToDto</c> projection so a new field added
/// to <see cref="SharedGameTranslation"/> can't drift between the list and the
/// detail endpoints.
/// </summary>
/// <remarks>
/// The <see cref="TranslationSourceMapper.ToPersistedString"/> round-trip keeps
/// the wire shape stable across enum extensions.
/// </remarks>
internal static class SharedGameTranslationProjections
{
    /// <summary>
    /// Compact projection used by the list endpoint and by
    /// <see cref="GameTitleResolver"/> when enriching <c>SharedGameDto</c>
    /// rows. Excludes audit + concurrency fields.
    /// </summary>
    public static SharedGameTranslationDto ToDto(SharedGameTranslation t) =>
        new(
            Locale: t.Locale.Value,
            Title: t.Title,
            Description: t.Description,
            Source: TranslationSourceMapper.ToPersistedString(t.Source));

    /// <summary>
    /// Full projection used by admin translation endpoints. Includes audit
    /// fields and the <c>Xmin</c> optimistic concurrency token.
    /// </summary>
    public static SharedGameTranslationDetailDto ToDetailDto(SharedGameTranslation t) =>
        new SharedGameTranslationDetailDto(
            Id: t.Id,
            GameId: t.SharedGameId,
            Locale: t.Locale.Value,
            Title: t.Title,
            Description: t.Description,
            Source: TranslationSourceMapper.ToPersistedString(t.Source),
            CreatedAt: t.CreatedAt,
            CreatedBy: t.CreatedBy,
            UpdatedAt: t.UpdatedAt,
            UpdatedBy: t.UpdatedBy,
            Xmin: t.Xmin);
}
