using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Application.EventHandlers;

/// <summary>
/// Handles TokenUsageLedgerEvent by creating a financial ledger entry.
/// Error-safe: catches and logs exceptions to avoid failing the main token tracking flow.
/// Issue #3721: Automatic Ledger Tracking (Epic #3688)
/// </summary>
internal sealed class TokenUsageLedgerEventHandler : INotificationHandler<TokenUsageLedgerEvent>
{
    private readonly ILedgerTrackingService _ledgerTrackingService;
    private readonly ILogger<TokenUsageLedgerEventHandler> _logger;

    public TokenUsageLedgerEventHandler(
        ILedgerTrackingService ledgerTrackingService,
        ILogger<TokenUsageLedgerEventHandler> logger)
    {
        _ledgerTrackingService = ledgerTrackingService ?? throw new ArgumentNullException(nameof(ledgerTrackingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenUsageLedgerEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _ledgerTrackingService.TrackTokenUsageAsync(
                userId: notification.UserId,
                modelId: notification.ModelId,
                tokensConsumed: notification.TokensConsumed,
                costUsd: notification.CostUsd,
                endpoint: notification.Endpoint,
                sourceEventId: notification.EventId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
        {
            // CF-2 / #1938: outbox replay (#1535 at-least-once delivery) for an event
            // already persisted. The partial UNIQUE index on ledger_entries.source_event_id
            // blocked the duplicate insert at the DB. Log at Information — a noisy
            // LogError on legitimate replays would crowd out real failures in operator
            // dashboards.
            _logger.LogInformation(
                ex,
                "Skipping duplicate ledger entry for token usage: User={UserId}, Model={ModelId}, SourceEventId={SourceEventId} (already recorded)",
                notification.UserId, notification.ModelId, notification.EventId);
        }
        catch (Exception ex)
        {
            // Log but don't rethrow - ledger tracking should never block the main flow
            _logger.LogError(
                ex,
                "Failed to create ledger entry for token usage: User={UserId}, Model={ModelId}, Cost=${Cost}",
                notification.UserId,
                notification.ModelId,
                notification.CostUsd);
        }
    }
}
