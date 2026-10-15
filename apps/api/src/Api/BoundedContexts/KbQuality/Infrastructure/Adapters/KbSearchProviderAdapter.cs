using System.Diagnostics;
using System.Globalization;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

/// <summary>
/// Adapter from KbQuality's <see cref="IKbSearchProvider"/> port onto the
/// KnowledgeBase BC's internal hybrid <see cref="SearchQuery"/> (#1675 Task 18).
///
/// <para>The KnowledgeBase BC keeps <see cref="SearchQuery"/> and <see cref="SearchResultDto"/>
/// <c>internal</c> by design — they participate in the in-process MediatR pipeline only.
/// KbQuality lives in the same Api assembly, so this adapter can construct the internal
/// query directly. Doing it through this single class keeps the cross-BC coupling localised
/// and reversible: if KB ever extracts to its own assembly, only this adapter changes.</para>
///
/// <para>Doc-scoped search: <see cref="SearchQuery"/> requires a <c>GameId</c> + accepts an
/// optional <c>PdfDocumentIds</c> filter. We resolve the game id from the
/// <see cref="Api.Infrastructure.Entities.PdfDocumentEntity"/> (<c>SharedGameId</c> first, else
/// <c>PrivateGameId</c>) and pin <c>PdfDocumentIds=[docId]</c> so chunks from sibling docs in the
/// same game are not retrievable. Search runs as the system (no UserId), since KbQuality is
/// admin-only and the RAG access service short-circuits when UserId is null.</para>
///
/// <para>Chunk-id contract (#4288): the port's <see cref="SearchResult.RetrievedChunkIds"/> are
/// <c>TextChunkEntity.Id</c> values, read from <see cref="SearchResultDto.TextChunkId"/>. Not from
/// <see cref="SearchResultDto.VectorDocumentId"/>, which never holds a chunk id: the vector arm puts
/// the vector document there and the keyword arm the PDF, so comparing it with the goldset's
/// <c>SourceChunkId</c> scored every evaluation zero. A result without a chunk id keeps its rank
/// as <c>null</c> instead of being skipped, so the results after it do not move up.</para>
/// </summary>
internal sealed class KbSearchProviderAdapter(IMediator mediator, MeepleAiDbContext db) : IKbSearchProvider
{
    public async Task<SearchResult> SearchAsync(Guid docId, string question, int topK, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        // Resolve gameId + language from the PdfDocument so the KB search has the context it
        // needs. AsNoTracking — we only project two cols, no domain operations.
        var pdfMeta = await db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == docId)
            .Select(p => new { p.SharedGameId, p.PrivateGameId, p.Language })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (pdfMeta is null)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"PDF {docId} not found in KbSearchProviderAdapter"));
        }

        var gameId = pdfMeta.SharedGameId ?? pdfMeta.PrivateGameId
            ?? throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture,
                    $"PDF {docId} has neither SharedGameId nor PrivateGameId — KbQuality eval requires a game association"));

        var sw = Stopwatch.StartNew();

        var query = new SearchQuery(
            GameId: gameId,
            Query: question,
            // Evaluation runs start from /admin/kb/docs/{docId}/evaluations (RequireAdminOrAbove).
            Access: SearchAccess.AlreadyChecked,
            TopK: topK,
            Language: pdfMeta.Language ?? "en",
            PdfDocumentIds: new[] { docId });

        var hits = await mediator.Send(query, ct).ConfigureAwait(false);

        sw.Stop();

        return new SearchResult(hits.Select(hit => hit.TextChunkId).ToList(), sw.Elapsed);
    }
}
