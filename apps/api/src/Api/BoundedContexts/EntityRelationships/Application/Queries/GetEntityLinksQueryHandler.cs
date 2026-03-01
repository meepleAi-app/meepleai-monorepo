using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.EntityRelationships.Application.Queries;

/// <summary>
/// Handler for GetEntityLinksQuery (Issue #5135).
///
/// Retrieves all EntityLinks for the given entity (as source or as target of
/// bidirectional links). Populates IsOwner on each DTO when RequestingUserId is provided.
/// When TargetEntityType=KbCard, enriches each link with KbCardStatus (Issue #5188).
/// </summary>
internal sealed class GetEntityLinksQueryHandler
    : IQueryHandler<GetEntityLinksQuery, IReadOnlyList<EntityLinkDto>>
{
    private readonly IEntityLinkRepository _repository;
    private readonly IMediator _mediator;

    public GetEntityLinksQueryHandler(IEntityLinkRepository repository, IMediator mediator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<IReadOnlyList<EntityLinkDto>> Handle(
        GetEntityLinksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var links = await _repository.GetForEntityAsync(
            query.EntityType,
            query.EntityId,
            query.Scope,
            query.LinkType,
            query.TargetEntityType,
            cancellationToken).ConfigureAwait(false);

        // Batch-fetch KB card statuses when needed (Issue #5188)
        IReadOnlyDictionary<Guid, PdfDocumentStatusResult> kbStatuses =
            new Dictionary<Guid, PdfDocumentStatusResult>();

        var kbCardLinks = links
            .Where(l => l.TargetEntityType == MeepleEntityType.KbCard)
            .ToList();

        if (kbCardLinks.Count > 0)
        {
            var pdfIds = kbCardLinks.Select(l => l.TargetEntityId).ToList();
            kbStatuses = await _mediator
                .Send(new GetPdfDocumentStatusesByIdsQuery(pdfIds), cancellationToken)
                .ConfigureAwait(false);
        }

        return links
            .Select(link =>
            {
                var dto = EntityLinkDto.FromEntity(link);

                if (query.RequestingUserId.HasValue)
                    dto = dto with { IsOwner = link.OwnerUserId == query.RequestingUserId.Value };

                // Enrich with KB card status if available (Issue #5188)
                if (link.TargetEntityType == MeepleEntityType.KbCard
                    && kbStatuses.TryGetValue(link.TargetEntityId, out var status))
                {
                    dto = dto with
                    {
                        KbCardStatus = new KbCardStatusDto(
                            FileName: status.FileName,
                            FileSizeBytes: status.FileSizeBytes,
                            ProcessingState: status.ProcessingState,
                            ProgressPercentage: status.ProgressPercentage,
                            CanRetry: status.CanRetry,
                            ErrorCategory: status.ErrorCategory,
                            ProcessingError: status.ProcessingError)
                    };
                }

                return dto;
            })
            .ToList()
            .AsReadOnly();
    }
}
