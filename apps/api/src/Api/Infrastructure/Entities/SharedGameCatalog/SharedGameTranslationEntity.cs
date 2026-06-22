namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for the <c>shared_game_translations</c> table.
/// Persistence model for the
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.SharedGameTranslation"/>
/// aggregate (issue #2339 — sub-PR 1/3).
/// </summary>
/// <remarks>
/// <para>
/// The canonical EN copy of a game is stored on <c>shared_games.title</c>/<c>description</c>.
/// This table only holds non-EN translations. A partial unique index
/// <c>uq_active_translation_per_locale</c> enforces "at most one active row
/// per (shared_game_id, locale)" via <c>WHERE NOT is_deleted</c>, allowing
/// re-creation after soft delete.
/// </para>
/// <para>
/// Optimistic concurrency uses PostgreSQL's <c>xmin</c> system column
/// (ADR-060 pattern). EF projects it as a <see cref="uint"/> concurrency token.
/// </para>
/// </remarks>
public class SharedGameTranslationEntity
{
    public Guid Id { get; set; }

    public Guid SharedGameId { get; set; }

    public string Locale { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Source identifier persisted as a lower-case string
    /// (<c>"manual"</c> | <c>"auto-openrouter"</c> | <c>"community"</c>).
    /// Repository mappers round-trip it against
    /// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.TranslationSource"/>.
    /// </summary>
    public string Source { get; set; } = "manual";

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// PostgreSQL <c>xmin</c> system column (ADR-060). Projected by EF Core
    /// as a concurrency token; updated by Postgres on every write, so client
    /// writes carrying a stale value trigger <c>DbUpdateConcurrencyException</c>.
    /// </summary>
    public uint Xmin { get; set; }

    /// <summary>
    /// Optional navigation back to the parent <see cref="SharedGameEntity"/>.
    /// Kept to make ad-hoc admin queries ergonomic; the aggregate does not
    /// depend on it.
    /// </summary>
    public SharedGameEntity? SharedGame { get; set; }
}
