using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfDocumentStatusesByIdsQuery (Issue #5188).
/// Batch-fetches PDF document status for KB card enrichment in EntityLink responses.
/// </summary>
internal sealed class GetPdfDocumentStatusesByIdsQueryHandler
    : IQueryHandler<GetPdfDocumentStatusesByIdsQuery, IReadOnlyDictionary<Guid, PdfDocumentStatusResult>>
{
    private readonly IPdfDocumentRepository _repository;

    public GetPdfDocumentStatusesByIdsQueryHandler(IPdfDocumentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyDictionary<Guid, PdfDocumentStatusResult>> Handle(
        GetPdfDocumentStatusesByIdsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.DocumentIds.Count == 0)
            return new Dictionary<Guid, PdfDocumentStatusResult>();

        var documents = await _repository
            .GetByIdsAsync(query.DocumentIds, cancellationToken)
            .ConfigureAwait(false);

        return documents.ToDictionary(
            doc => doc.Id,
            doc => new PdfDocumentStatusResult(
                FileName: doc.FileName.Value,
                FileSizeBytes: doc.FileSize.Bytes,
                ProcessingState: doc.ProcessingState.ToString(),
                ProgressPercentage: doc.ProgressPercentage,
                CanRetry: doc.CanRetry(),
                ErrorCategory: doc.ErrorCategory?.ToString(),
                ProcessingError: doc.ProcessingError
            ));
    }
}
