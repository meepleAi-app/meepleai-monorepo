using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetDocumentsBySharedGameQuery.
/// </summary>
internal sealed class GetDocumentsBySharedGameQueryHandler
    : IQueryHandler<GetDocumentsBySharedGameQuery, IReadOnlyList<SharedGameDocumentDto>>
{
    private readonly ISharedGameDocumentRepository _repository;
    private readonly ILogger<GetDocumentsBySharedGameQueryHandler> _logger;

    public GetDocumentsBySharedGameQueryHandler(
        ISharedGameDocumentRepository repository,
        ILogger<GetDocumentsBySharedGameQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SharedGameDocumentDto>> Handle(
        GetDocumentsBySharedGameQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting documents for shared game {SharedGameId}, Type filter: {DocumentType}",
            query.SharedGameId, query.DocumentType);

        IReadOnlyList<SharedGameDocument> documents;

        if (query.DocumentType.HasValue)
        {
            documents = await _repository.GetBySharedGameIdAndTypeAsync(
                query.SharedGameId,
                query.DocumentType.Value,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            documents = await _repository.GetBySharedGameIdAsync(
                query.SharedGameId,
                cancellationToken).ConfigureAwait(false);
        }

        return documents.Select(MapToDto).ToList();
    }

    private static SharedGameDocumentDto MapToDto(SharedGameDocument document)
    {
        return new SharedGameDocumentDto(
            document.Id,
            document.SharedGameId,
            document.PdfDocumentId,
            document.DocumentType,
            document.Version,
            document.IsActive,
            document.Tags,
            document.CreatedAt,
            document.CreatedBy);
    }
}
