using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Maps <see cref="SharedGameTranslation"/> aggregates to the read-side
/// <see cref="SharedGameTranslationDetailDto"/> surface used by the admin
/// translation endpoints. Issue #2339 — sub-PR 1/3 Wave 4 (Task 12).
/// </summary>
/// <remarks>
/// Plan Task 12 explicitly calls out the read-query handlers' duplication of
/// the projection logic; this mapper resolves that DRY concern. The
/// <c>Source</c> enum is round-tripped through
/// <see cref="TranslationSourceMapper.ToPersistedString"/> so the wire shape
/// stays stable across enum extensions.
/// </remarks>
internal static class SharedGameTranslationMapper
{
    public static SharedGameTranslationDetailDto ToDetailDto(SharedGameTranslation t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new SharedGameTranslationDetailDto(
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
}
