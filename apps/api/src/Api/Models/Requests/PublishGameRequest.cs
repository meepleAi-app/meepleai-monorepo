using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.Models.Requests;

/// <summary>
/// Request model for publishing game to SharedGameCatalog.
/// Issue #3481: Admin publication workflow.
/// </summary>
public sealed record PublishGameRequest
{
    /// <summary>
    /// Target approval status for the game.
    /// </summary>
    public required ApprovalStatus Status { get; init; }
}
