using Api.Infrastructure;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Resolves the GameId to use as <c>text_chunks.GameId</c> for a <see cref="PdfDocumentEntity"/>.
/// </summary>
/// <remarks>
/// Post-Phase2d (Issue #1345): the legacy <c>games</c> table is gone; the value formerly known
/// as <c>games.Id</c> is now equivalent to <c>shared_games.id</c>. The resolver therefore returns
/// the PDF's <c>SharedGameId</c> (or <c>PrivateGameId</c> for private PDFs) directly.
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
