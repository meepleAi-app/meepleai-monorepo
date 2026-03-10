using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

/// <summary>
/// Batch command to add RAG to multiple SharedGames.
/// Processes items sequentially to avoid overwhelming the upload pipeline.
/// Supports partial success — individual failures don't stop the batch.
/// </summary>
internal record BatchAddRagToSharedGameCommand(
    List<AddRagToSharedGameCommand> Items
) : ICommand<BatchAddRagToSharedGameResult>;

internal record BatchAddRagToSharedGameResult(
    List<BatchItemResult> Results,
    int SuccessCount,
    int FailureCount
);

internal record BatchItemResult(
    Guid SharedGameId,
    string FileName,
    AddRagToSharedGameResult? Result,
    string? Error
);
