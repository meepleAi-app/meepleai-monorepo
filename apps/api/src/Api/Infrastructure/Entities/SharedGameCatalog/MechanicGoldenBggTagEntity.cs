namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicGoldenBggTag"/>.
/// Plain POCO — all invariants are enforced by the domain entity; this type exists only to be shaped by EF Core.
/// Rows follow an append/replace pattern (no optimistic concurrency token needed).
/// </summary>
public class MechanicGoldenBggTagEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }

    // === Navigation ===
    public SharedGameEntity SharedGame { get; set; } = default!;
}
