using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Persistence entity for GameFAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// Uses optimistic concurrency control via RowVersion to prevent race conditions.
/// </summary>
public class GameFAQEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Optimistic concurrency control (Issue #2028: Code review fix)
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation property
    public GameEntity? Game { get; set; }
}
