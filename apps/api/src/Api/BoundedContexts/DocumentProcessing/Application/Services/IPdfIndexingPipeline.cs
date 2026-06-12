using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Single owner of the VectorDocument construction + persistence + event-raising flow.
/// Replaces the duplicated <c>new VectorDocumentEntity {...}</c> anti-pattern in 3 ingestion
/// paths (#2244 / epic #2242 Sub #2). The returned aggregate already carries
/// <see cref="KnowledgeBase.Domain.Events.VectorDocumentIndexedEvent"/> via the
/// <see cref="VectorDocument.Create"/> factory; the repository collects it and the DbContext
/// SaveChanges dispatcher publishes it through MediatR.
/// </summary>
internal interface IPdfIndexingPipeline
{
    Task<VectorDocument> ExecuteAsync(
        PdfDocumentEntity pdfDoc,
        int indexedChunkCount,
        Guid resolvedGameId,
        CancellationToken cancellationToken = default);
}
