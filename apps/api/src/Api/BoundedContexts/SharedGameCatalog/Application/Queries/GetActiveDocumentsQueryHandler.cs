using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetActiveDocumentsQuery.
/// </summary>
internal sealed class GetActiveDocumentsQueryHandler
    : IQueryHandler<GetActiveDocumentsQuery, IReadOnlyList<SharedGameDocumentDto>>
{
    private readonly ISharedGameDocumentRepository _repository;
    private readonly ILogger<GetActiveDocumentsQueryHandler> _logger;

    public GetActiveDocumentsQueryHandler(
        ISharedGameDocumentRepository repository,
        ILogger<GetActiveDocumentsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SharedGameDocumentDto>> Handle(
        GetActiveDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting active documents for shared game {SharedGameId}",
            query.SharedGameId);

        var documents = await _repository.GetActiveDocumentsAsync(
            query.SharedGameId,
            cancellationToken).ConfigureAwait(false);

        return documents.Select(d => new SharedGameDocumentDto(
            d.Id,
            d.SharedGameId,
            d.PdfDocumentId,
            d.DocumentType,
            d.Version,
            d.IsActive,
            d.Tags,
            d.CreatedAt,
            d.CreatedBy)).ToList();
    }
}
