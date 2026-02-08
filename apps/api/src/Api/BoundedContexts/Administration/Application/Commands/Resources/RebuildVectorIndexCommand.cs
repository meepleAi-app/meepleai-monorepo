using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Command to rebuild Qdrant vector index for a specific collection.
/// DANGER: This will recreate the entire vector index and may take significant time.
/// Issue #3695: Resources Monitoring - Rebuild vector index action (Level 2 confirmation required)
/// </summary>
internal record RebuildVectorIndexCommand(
    string CollectionName,
    bool Confirmed = false
) : ICommand<bool>;
