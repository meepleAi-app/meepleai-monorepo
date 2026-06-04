using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for <see cref="EnqueueCatalogSeedCommand"/>.
/// Idempotent on <c>BggId</c>: if a draft already exists, no new row is inserted
/// and the existing draft is returned with <c>IsDuplicate=true</c>.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.2.
/// </summary>
internal sealed class EnqueueCatalogSeedCommandHandler
    : ICommandHandler<EnqueueCatalogSeedCommand, EnqueueCatalogSeedResult>
{
    private readonly ICatalogSeedDraftRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnqueueCatalogSeedCommandHandler> _logger;

    public EnqueueCatalogSeedCommandHandler(
        ICatalogSeedDraftRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<EnqueueCatalogSeedCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnqueueCatalogSeedResult> Handle(
        EnqueueCatalogSeedCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BggId is int bggId)
        {
            var exists = await _repository
                .ExistsAsync(bggId, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
            {
                _logger.LogInformation(
                    "CatalogSeedDraft duplicate detected for BggId={BggId}; skipping insert.",
                    bggId);
                return new EnqueueCatalogSeedResult(Guid.Empty, bggId, nameof(CatalogSeedStatus.Pending), IsDuplicate: true);
            }
        }

        var entity = new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = request.BggId,
            WikidataQid = string.IsNullOrWhiteSpace(request.WikidataQid) ? null : request.WikidataQid,
            SearchTermInput = string.IsNullOrWhiteSpace(request.SearchTermInput) ? null : request.SearchTermInput,
            Status = nameof(CatalogSeedStatus.Pending),
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "CatalogSeedDraft enqueued Id={DraftId} BggId={BggId} Wikidata={Qid}",
            entity.Id,
            entity.BggId,
            entity.WikidataQid);

        return new EnqueueCatalogSeedResult(entity.Id, entity.BggId, entity.Status, IsDuplicate: false);
    }
}
