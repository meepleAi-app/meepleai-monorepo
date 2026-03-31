using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Represents a default phase template for a game turn (e.g. "Draw cards", "Trade", "Build").
/// Configured by Editors/Admins per game; pre-loaded when creating a live session.
/// </summary>
internal sealed class GamePhaseTemplate : Entity<Guid>
{
    public Guid GameId { get; private set; }
    public string PhaseName { get; private set; } = string.Empty;
    public int PhaseOrder { get; private set; }
    public string? Description { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private GamePhaseTemplate() { }
#pragma warning restore CS8618

    public static GamePhaseTemplate Create(
        Guid gameId,
        string phaseName,
        int phaseOrder,
        string createdBy,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new GamePhaseTemplate
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PhaseName = phaseName.Trim(),
            PhaseOrder = phaseOrder,
            Description = description?.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Restores an entity from database — preserves original Id/CreatedAt.
    /// Used exclusively by the repository.
    /// </summary>
    internal static GamePhaseTemplate Restore(
        Guid id, Guid gameId, string phaseName, int phaseOrder,
        string createdBy, string? description, DateTime createdAt, DateTime updatedAt)
    {
        return new GamePhaseTemplate
        {
            Id = id,
            GameId = gameId,
            PhaseName = phaseName,
            PhaseOrder = phaseOrder,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Update(string phaseName, int phaseOrder, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        PhaseName = phaseName.Trim();
        PhaseOrder = phaseOrder;
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
