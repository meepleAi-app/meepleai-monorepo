using Api.Infrastructure;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// The game a <see cref="PdfDocumentEntity"/> belongs to: its private game if it has one,
/// otherwise its shared game.
/// </summary>
/// <remarks>
/// <para>
/// Post-Phase2d (Issue #1345): the legacy <c>games</c> table is gone; the value formerly known
/// as <c>games.Id</c> is now equivalent to <c>shared_games.id</c>. The resolver therefore returns
/// the PDF's <c>SharedGameId</c> (or <c>PrivateGameId</c> for private PDFs) directly.
/// </para>
/// <para>
/// Issue #4244: this is the id a question about the game is asked with (cache keys, the
/// <c>game_id</c> of the embeddings). It is NOT the value of <c>text_chunks.GameId</c> or
/// <c>vector_documents.GameId</c>: those are foreign keys to <c>shared_games</c>, and writing a
/// private game's id there failed the save. Those two columns take <c>pdf.SharedGameId</c>, and
/// the private game goes in their <c>PrivateGameId</c> column.
/// </para>
/// </remarks>
public static class PdfGameIdResolver
{
    public static Task<Guid?> ResolveAsync(
        MeepleAiDbContext db,
        PdfDocumentEntity pdf,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pdf);

        if (pdf.PrivateGameId.HasValue)
        {
            return Task.FromResult<Guid?>(pdf.PrivateGameId.Value);
        }

        return Task.FromResult<Guid?>(pdf.SharedGameId);
    }
}
