using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to batch reject multiple games at once.
/// Issue #3350: Batch Approval/Rejection for Games
/// </summary>
internal record BatchRejectGamesCommand(
    IReadOnlyList<Guid> GameIds,
    Guid RejectedBy,
    string Reason
) : ICommand<BatchRejectGamesResult>;

/// <summary>
/// Result of batch reject operation.
/// </summary>
internal record BatchRejectGamesResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors
);
