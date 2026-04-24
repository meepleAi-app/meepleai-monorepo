namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicGoldenClaim"/>.
/// Plain POCO — all invariants are enforced by the domain aggregate; this type exists only to be shaped by EF Core.
/// </summary>
public class MechanicGoldenClaimEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }

    /// <summary>0=Summary, 1=Mechanics, 2=Victory, 3=Resources, 4=Phases, 5=Faq.
    /// Mirrors <c>Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicSection</c>.</summary>
    public int Section { get; set; }

    public string Statement { get; set; } = string.Empty;
    public int ExpectedPage { get; set; }
    public string SourceQuote { get; set; } = string.Empty;

    /// <summary>Stored as JSON array (jsonb column).</summary>
    public string KeywordsJson { get; set; } = "[]";

    /// <summary>768-dimensional sentence-transformer vector; null until embedded.</summary>
    public float[]? Embedding { get; set; }

    public Guid CuratorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // === Soft delete ===
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // === Optimistic concurrency ===
    /// <summary>
    /// PostgreSQL system column <c>xmin</c> mapped as <see cref="uint"/> (xid).
    /// Marked as concurrency token in EF configuration; server-generated on add/update.
    /// </summary>
    public uint Xmin { get; set; }

    // === Navigation ===
    public SharedGameEntity SharedGame { get; set; } = default!;
}
