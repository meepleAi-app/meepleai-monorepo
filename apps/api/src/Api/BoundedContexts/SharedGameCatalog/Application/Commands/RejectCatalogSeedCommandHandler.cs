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
/// Handler for <see cref="RejectCatalogSeedCommand"/>.
/// Soft-deletes a draft (audit-retained) and publishes
/// <see cref="CatalogSeedRejectedEvent"/> via MediatR <em>after</em> the save
/// commits — post-save publish pattern (CLAUDE.md / issue #1873 H1) so that a
/// stale event from a failed SaveChangesAsync is never replayed by a later
/// save on the same scope.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.5.
/// </summary>
internal sealed class RejectCatalogSeedCommandHandler : ICommandHandler<RejectCatalogSeedCommand>
{
    private readonly ICatalogSeedDraftRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<RejectCatalogSeedCommandHandler> _logger;

    public RejectCatalogSeedCommandHandler(
        ICatalogSeedDraftRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RejectCatalogSeedCommandHandler> logger,
        IMediator mediator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task Handle(RejectCatalogSeedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var draft = await _repository
            .GetByIdAsync(request.DraftId, cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException("CatalogSeedDraft", request.DraftId.ToString());
        }

        draft.Status = nameof(CatalogSeedStatus.Rejected);
        draft.IsDeleted = true;
        draft.DeletedAt = DateTime.UtcNow;

        // Persist reason for audit + DB-level recoverability even if no event handler runs.
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            var reason = request.Reason.Length > 500 ? request.Reason[..500] : request.Reason;
            draft.ErrorMessage = reason;
        }

        // Repository was created via AsTracking() GetByIdAsync so SaveChangesAsync
        // will persist these mutations without an explicit Update() call
        // (CLAUDE.md memory: notracking-default-update-gotcha).
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "CatalogSeedDraft {DraftId} rejected by user {UserId}",
            request.DraftId,
            request.RejectedByUserId);

        await _mediator
            .Publish(
                new CatalogSeedRejectedEvent(request.DraftId, request.RejectedByUserId, request.Reason ?? string.Empty),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
