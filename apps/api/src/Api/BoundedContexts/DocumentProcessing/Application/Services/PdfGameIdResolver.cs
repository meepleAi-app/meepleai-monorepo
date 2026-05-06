using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Resolves the <c>games.Id</c> to use as <c>text_chunks.GameId</c> for a <see cref="PdfDocumentEntity"/>.
/// </summary>
/// <remarks>
/// <c>text_chunks.GameId</c> is a foreign key to <c>games.Id</c>, NOT to <c>shared_games.id</c>.
/// Naively using <c>pdf.SharedGameId</c> as the chunk's GameId triggers
/// <c>FK_text_chunks_games_GameId</c> violations whenever the shared catalog id and the games row id
/// differ (the common case once Issue #2373 wiring runs).
///
/// Resolution strategy:
/// - Private PDF: return <c>PrivateGameId</c> directly (it already references <c>games.Id</c>'s
///   private-game peer table, which the EF model treats as a separate FK target).
/// - Shared PDF: look up the <c>GameEntity</c> whose <c>SharedGameId</c> matches the PDF's shared-game id.
/// - Neither set, or no matching games row: return <c>null</c> (callers decide whether to default to
///   <see cref="System.Guid.Empty"/> or skip persistence).
/// </remarks>
public static class PdfGameIdResolver
{
    public static async Task<Guid?> ResolveAsync(
        MeepleAiDbContext db,
        PdfDocumentEntity pdf,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pdf);

        if (pdf.PrivateGameId.HasValue)
        {
            return pdf.PrivateGameId.Value;
        }

        if (!pdf.SharedGameId.HasValue)
        {
            return null;
        }

        return await db.Games
            .Where(g => g.SharedGameId == pdf.SharedGameId.Value)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
