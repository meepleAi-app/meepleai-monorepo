using Api.BoundedContexts.Administration.Domain.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.DomainEventOutbox;

/// <summary>
/// Handler for <see cref="RetryEventOutboxRowCommand"/>.
///
/// <para>Loads the row tracked, invokes
/// <see cref="DomainEventOutboxEntity.RearmFromFailed"/>, and saves. The entity
/// itself guards the transition (must currently be Failed), so this handler is
/// thin — just lookup + delegate + persist.</para>
///
/// Issue #1535 T6.
/// </summary>
internal sealed class RetryEventOutboxRowCommandHandler
    : ICommandHandler<RetryEventOutboxRowCommand, bool>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public RetryEventOutboxRowCommandHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<bool> Handle(
        RetryEventOutboxRowCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // AsTracking is REQUIRED: MeepleAiDbContext defaults to NoTracking
        // (PERF-06), so a vanilla FirstOrDefaultAsync would return an entity
        // whose mutation (RearmFromFailed) is invisible to SaveChangesAsync.
        var row = await _db.DomainEventOutbox
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            // Caller (endpoint) translates false → 404.
            return false;
        }

        // Pre-check: translate the entity's broad InvalidOperationException into a
        // dedicated domain exception. Issue #1535 T6 code review (F5) flagged that
        // catching IOEx at the endpoint masked unrelated errors (EF concurrency, MediatR
        // pipeline) as 409 'cannot re-arm'. The narrow OutboxRowNotFailedException carries
        // the row id + current status for an unambiguous Problem detail.
        if (row.Status != DomainEventOutboxStatus.Failed)
        {
            throw new OutboxRowNotFailedException(row.Id, row.Status.ToString());
        }

        row.RearmFromFailed(_timeProvider.GetUtcNow());

        // F11: bypass the overridden SaveChangesAsync to skip the domain-event routing
        // pipeline. Without this guard, any event accidentally left in
        // IDomainEventCollector by an upstream behavior would be enqueued as a phantom
        // outbox row as a side-effect of the rearm.
        await _db.SaveChangesWithoutDomainEventDispatchAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
