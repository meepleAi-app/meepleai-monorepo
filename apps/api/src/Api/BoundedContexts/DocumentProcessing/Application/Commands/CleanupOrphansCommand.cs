using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to cleanup orphaned text chunks and vectors referencing non-existent PDFs.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal record CleanupOrphansCommand() : ICommand<CleanupOrphansResult>;

internal record CleanupOrphansResult(int OrphanedChunks, int OrphanedVectors);
