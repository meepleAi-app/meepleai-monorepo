namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for game phase templates.
/// </summary>
public class GamePhaseTemplateEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string PhaseName { get; set; } = default!;
    public int PhaseOrder { get; set; }
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public GameEntity? Game { get; set; }
}
