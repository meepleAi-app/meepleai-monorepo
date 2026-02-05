using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to batch approve multiple games at once.
/// Issue #3350: Batch Approval/Rejection for Games
/// </summary>
internal record BatchApproveGamesCommand(
    IReadOnlyList<Guid> GameIds,
    Guid ApprovedBy,
    string? Note
) : ICommand<BatchApproveGamesResult>;

/// <summary>
/// Result of batch approve operation.
/// </summary>
internal record BatchApproveGamesResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors
);
