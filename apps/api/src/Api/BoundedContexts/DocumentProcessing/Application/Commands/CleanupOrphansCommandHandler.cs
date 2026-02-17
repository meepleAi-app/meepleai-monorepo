using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for CleanupOrphansCommand.
/// Finds and removes text chunks that reference non-existent PDF documents.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal sealed class CleanupOrphansCommandHandler
    : ICommandHandler<CleanupOrphansCommand, CleanupOrphansResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public CleanupOrphansCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CleanupOrphansResult> Handle(
        CleanupOrphansCommand command, CancellationToken cancellationToken)
    {
        // Find orphaned text chunks (chunks whose PdfDocumentId doesn't exist in PdfDocuments)
        var validDocIds = _dbContext.PdfDocuments.Select(p => p.Id);

        var orphanedChunks = await _dbContext.TextChunks
            .Where(tc => !validDocIds.Contains(tc.PdfDocumentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var orphanedChunkCount = orphanedChunks.Count;

        if (orphanedChunks.Count > 0)
        {
            _dbContext.TextChunks.RemoveRange(orphanedChunks);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new CleanupOrphansResult(orphanedChunkCount, 0);
    }
}
