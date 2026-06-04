using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for <see cref="BulkEnqueueCatalogSeedsCommand"/>.
/// Delegates per-item dispatch to <see cref="EnqueueCatalogSeedCommand"/> via MediatR
/// so idempotency, validation, and persistence semantics stay in a single place.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.3.
/// </summary>
internal sealed class BulkEnqueueCatalogSeedsCommandHandler
    : ICommandHandler<BulkEnqueueCatalogSeedsCommand, BulkEnqueueCatalogSeedsResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<BulkEnqueueCatalogSeedsCommandHandler> _logger;

    public BulkEnqueueCatalogSeedsCommandHandler(
        IMediator mediator,
        ILogger<BulkEnqueueCatalogSeedsCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkEnqueueCatalogSeedsResult> Handle(
        BulkEnqueueCatalogSeedsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var newIds = new List<Guid>(request.BggIds.Count);
        var enqueued = 0;
        var duplicates = 0;

        foreach (var bggId in request.BggIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inner = new EnqueueCatalogSeedCommand(bggId, null, null, request.CreatedByUserId);
            var result = await _mediator.Send(inner, cancellationToken).ConfigureAwait(false);

            if (result.IsDuplicate)
            {
                duplicates++;
            }
            else
            {
                enqueued++;
                newIds.Add(result.Id);
            }
        }

        _logger.LogInformation(
            "BulkEnqueueCatalogSeeds processed total={Total} enqueued={Enqueued} duplicates={Duplicates}",
            request.BggIds.Count,
            enqueued,
            duplicates);

        return new BulkEnqueueCatalogSeedsResult(
            Total: request.BggIds.Count,
            Enqueued: enqueued,
            Duplicates: duplicates,
            NewDraftIds: newIds);
    }
}
