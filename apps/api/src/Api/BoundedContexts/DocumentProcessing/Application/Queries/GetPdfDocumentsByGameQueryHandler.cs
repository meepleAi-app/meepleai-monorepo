using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal class GetPdfDocumentsByGameQueryHandler : IQueryHandler<GetPdfDocumentsByGameQuery, IReadOnlyList<PdfDocumentDto>>
{
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly IPrivateGameRepository _privateGameRepository;

    public GetPdfDocumentsByGameQueryHandler(
        IPdfDocumentRepository documentRepository,
        IPrivateGameRepository privateGameRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
    }

    public async Task<IReadOnlyList<PdfDocumentDto>> Handle(GetPdfDocumentsByGameQuery query, CancellationToken cancellationToken)
    {
        // #4348: un gioco privato tiene i suoi PDF in `PrivateGameId`, che `FindByGameIdAsync` non
        // guarda, e li vede solo il proprietario. Per chiunque altro risponde come un gioco
        // inesistente — una lista vuota, la stessa di prima —, che non dice se il gioco esiste: come
        // lo stato della KB (#4137).
        if (query.RequestingUserId is { } requestingUserId)
        {
            var privateGame = await _privateGameRepository.GetByIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
            if (privateGame is not null)
            {
                if (privateGame.OwnerId != requestingUserId)
                {
                    return Array.Empty<PdfDocumentDto>();
                }

                var own = await _documentRepository.FindByPrivateGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
                return own.Select(MapToDto).ToList();
            }
        }

        var documents = await _documentRepository.FindByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        return documents.Select(MapToDto).ToList();
    }

    private static PdfDocumentDto MapToDto(PdfDocument doc)
    {
        return new PdfDocumentDto(
            Id: doc.Id,
            GameId: doc.GameId,
            FileName: doc.FileName.Value,
            FilePath: doc.FilePath,
            FileSizeBytes: doc.FileSize.Bytes,
            UploadedAt: doc.UploadedAt,
            ProcessedAt: doc.ProcessedAt,
            PageCount: doc.PageCount,
            DocumentType: doc.DocumentType?.Value ?? "base", // Issue #2051
            ProcessingState: doc.ProcessingState.ToString(),
            ProgressPercentage: doc.ProgressPercentage,
            RetryCount: doc.RetryCount,
            MaxRetries: doc.MaxRetries,
            CanRetry: doc.CanRetry(),
            ErrorCategory: doc.ErrorCategory?.ToString(),
            ProcessingError: doc.ProcessingError,
            DocumentCategory: doc.DocumentCategory.ToString(),
            BaseDocumentId: doc.BaseDocumentId,
            IsActiveForRag: doc.IsActiveForRag,
            HasAcceptedDisclaimer: doc.HasAcceptedDisclaimer,
            VersionLabel: doc.VersionLabel
        );
    }
}
