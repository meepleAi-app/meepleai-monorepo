using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to mark documents stuck in processing (>24h) as failed.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal record PurgeStaleDocumentsCommand() : ICommand<PurgeStaleResult>;

internal record PurgeStaleResult(int PurgedCount);
