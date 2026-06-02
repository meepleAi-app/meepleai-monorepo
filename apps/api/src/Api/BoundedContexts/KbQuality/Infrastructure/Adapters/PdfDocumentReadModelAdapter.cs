using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

/// <summary>
/// Read-only projection of a PDF + its top-N chunks for the goldset generator (#1675 Task 18).
///
/// <para>Plan amendment A3: the schema column names differ from the plan text —
/// <see cref="TextChunkEntity.ChunkIndex"/> (NOT <c>Position</c>) and
/// <see cref="TextChunkEntity.Content"/> (NOT <c>Snippet</c>). This adapter does the rename
/// at the BC boundary so the rest of KbQuality can stay on the cleaner port vocabulary,
/// and truncates <c>Content</c> to <see cref="SnippetMaxChars"/> for the goldset prompt
/// (keeps the LLM under a tight per-chunk token cap).</para>
///
/// <para>Top-N selection: we pick chunks ordered by <c>ChunkIndex ASC</c> (i.e. document
/// reading order). The goldset generator's prompt is "produce Q&amp;A pairs answerable from
/// THIS chunk", so the choice of which chunks to use is orthogonal to retrieval ranking —
/// reading order is a stable, reproducible pick.</para>
/// </summary>
internal sealed class PdfDocumentReadModelAdapter(MeepleAiDbContext db) : IPdfDocumentReadModel
{
    /// <summary>How many chunks the goldset generator works against per doc.</summary>
    private const int TopChunksForGoldset = 5;

    /// <summary>Cap on snippet length sent to the LLM goldset prompt (~250-300 tokens).</summary>
    private const int SnippetMaxChars = 1200;

    public async Task<PdfDocSnapshot?> GetSnapshotAsync(Guid docId, CancellationToken ct)
    {
        var pdf = await db.PdfDocuments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == docId, ct)
            .ConfigureAwait(false);
        if (pdf is null)
        {
            return null;
        }

        var topChunks = await db.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == docId)
            .OrderBy(c => c.ChunkIndex)
            .Take(TopChunksForGoldset)
            .Select(c => new { c.Id, c.ChunkIndex, c.Content })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var totalChunks = await db.TextChunks
            .CountAsync(c => c.PdfDocumentId == docId, ct)
            .ConfigureAwait(false);

        var chunkSnapshots = topChunks
            .Select(c => new ChunkSnapshot(c.Id, c.ChunkIndex, TruncateSnippet(c.Content)))
            .ToList();

        return new PdfDocSnapshot(
            Id: pdf.Id,
            FileName: pdf.FileName,
            ChunkCount: totalChunks,
            ProcessingState: pdf.ProcessingState,
            TopChunks: chunkSnapshots);
    }

    private static string TruncateSnippet(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= SnippetMaxChars)
        {
            return content;
        }

        return content.AsSpan(0, SnippetMaxChars).ToString() + "…";
    }
}
