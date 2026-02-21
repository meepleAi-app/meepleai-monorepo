using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for TurnOrder.
/// Stores player order as JSON array in PostgreSQL.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
public class TurnOrderEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    /// <summary>Player order stored as JSON array.</summary>
    public string PlayerOrderJson { get; set; } = "[]";

    public int CurrentIndex { get; set; }
    public int RoundNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
