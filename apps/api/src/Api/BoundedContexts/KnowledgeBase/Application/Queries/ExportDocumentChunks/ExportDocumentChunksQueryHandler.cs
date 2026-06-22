using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ExportDocumentChunks;

/// <summary>
/// Handles <see cref="ExportDocumentChunksQuery"/> — returns ALL chunks for a document
/// with full content (no snippet truncation) ordered by ChunkIndex ascending.
/// Issue #1653: F3-FU-4 — Export document chunks (full content).
/// </summary>
internal sealed class ExportDocumentChunksQueryHandler
    : IQueryHandler<ExportDocumentChunksQuery, IReadOnlyList<ExportedChunkDto>>
{
    private readonly MeepleAiDbContext _db;

    public ExportDocumentChunksQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<ExportedChunkDto>> Handle(
        ExportDocumentChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await _db.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.PdfDocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new ExportedChunkDto(c.Id, c.ChunkIndex, c.PageNumber, c.Heading, c.Content))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
