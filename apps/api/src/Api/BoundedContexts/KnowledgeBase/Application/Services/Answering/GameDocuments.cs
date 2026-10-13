using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// The documents of a game for the paths that answer a question, whether the game is in the shared
/// catalogue or private.
/// </summary>
/// <remarks>
/// <para>
/// Issue #4244. The "by game" methods of the two repositories know shared games only: the
/// documents of a private game sit under <c>PrivateGameId</c>. A game is one or the other, hence a
/// second lookup only when the first finds nothing.
/// </para>
/// <para>
/// <b>Only for callers that have already passed the RAG access check for this game.</b> The lookup
/// is not folded into the repository methods on purpose: those also serve routes that check no
/// ownership (<c>GET /games/{id}/pdfs</c>, <c>GET /games/{id}/knowledge-base</c>), and widening
/// them would tell anyone who knows the id which documents a private game has.
/// </para>
/// <para>
/// Extension methods, not interface members: they go through the repository methods, so the test
/// doubles that configure those keep driving the lookup.
/// </para>
/// </remarks>
internal static class GameDocuments
{
    /// <summary>The PDFs of the game, shared or private.</summary>
    public static async Task<IReadOnlyList<PdfDocument>> FindPdfsOfGameAfterAccessCheckAsync(
        this IPdfDocumentRepository pdfDocuments,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdfDocuments);

        var ofSharedGame = await pdfDocuments.FindByGameIdAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (ofSharedGame is { Count: > 0 })
        {
            return ofSharedGame;
        }

        return await pdfDocuments.FindByPrivateGameIdAsync(gameId, cancellationToken).ConfigureAwait(false) ?? [];
    }

    /// <summary>The vector documents of the game, shared or private.</summary>
    public static async Task<IReadOnlyList<VectorDocument>> FindVectorDocumentsOfGameAfterAccessCheckAsync(
        this IVectorDocumentRepository vectorDocuments,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vectorDocuments);

        var ofSharedGame = await vectorDocuments.GetByGameIdAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (ofSharedGame is { Count: > 0 })
        {
            return ofSharedGame;
        }

        return await vectorDocuments.GetByPrivateGameIdAsync(gameId, cancellationToken).ConfigureAwait(false) ?? [];
    }
}
