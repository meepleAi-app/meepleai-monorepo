using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence model for <see cref="GameCoverAssignment"/> (epic #3470, Slice 1b).
/// An admin's per-context cover choice for a game: which source is pinned for a
/// given UI context and the crop focal point. At most one row per
/// (SharedGameId, Context). The generated per-context WebP crop key is stored
/// once rendered.
/// </summary>
public class GameCoverAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public CoverContext Context { get; set; }
    public CoverAssignmentSource Source { get; set; }
    public double FocalX { get; set; }
    public double FocalY { get; set; }
    public string? GeneratedR2Key { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public SharedGameEntity SharedGame { get; set; } = default!;
}
