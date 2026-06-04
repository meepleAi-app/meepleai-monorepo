using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for <see cref="ApproveCatalogSeedCommand"/>.
/// Marks a Fetched draft as Approved, persists the change, then publishes
/// <see cref="CatalogSeedApprovedEvent"/> via MediatR <em>after</em> the save
/// commits. Post-save publish follows the issue #1873 review-fix H1 pattern
/// (CLAUDE.md): events left in IDomainEventCollector after a save failure would
/// otherwise be drained by a subsequent SaveChangesAsync on the same scope,
/// triggering downstream side effects for a state that never persisted.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.4.
/// </summary>
internal sealed class ApproveCatalogSeedCommandHandler
    : ICommandHandler<ApproveCatalogSeedCommand, ApproveCatalogSeedResult>
{
    private readonly ICatalogSeedDraftRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator? _mediator;
    private readonly ILogger<ApproveCatalogSeedCommandHandler> _logger;

    public ApproveCatalogSeedCommandHandler(
        ICatalogSeedDraftRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ApproveCatalogSeedCommandHandler> logger,
        IMediator? mediator = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator;
    }

    public async Task<ApproveCatalogSeedResult> Handle(
        ApproveCatalogSeedCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var draft = await _repository
            .GetByIdAsync(request.DraftId, cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException("CatalogSeedDraft", request.DraftId.ToString());
        }

        if (!string.Equals(draft.Status, nameof(CatalogSeedStatus.Fetched), StringComparison.Ordinal))
        {
            throw new ConflictException(
                $"Draft {request.DraftId} is in status {draft.Status}; only Fetched drafts can be approved.");
        }

        // Placeholder; the actual SharedGameCatalogEntry row will be created by
        // CatalogSeedApprovedEventHandler in M5, which will overwrite this id
        // with the real entry id once materialization succeeds.
        var resultingSharedGameId = Guid.NewGuid();

        draft.Status = nameof(CatalogSeedStatus.Approved);
        draft.ApprovedAt = DateTime.UtcNow;
        draft.ApprovedByUserId = request.ApprovedByUserId;
        draft.ResultingSharedGameId = resultingSharedGameId;

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "CatalogSeedDraft {DraftId} approved by user {UserId} placeholder SharedGameId={SharedGameId}",
            request.DraftId,
            request.ApprovedByUserId,
            resultingSharedGameId);

        if (_mediator is not null)
        {
            await _mediator
                .Publish(
                    new CatalogSeedApprovedEvent(request.DraftId, resultingSharedGameId, request.ApprovedByUserId),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new ApproveCatalogSeedResult(request.DraftId, resultingSharedGameId);
    }
}
