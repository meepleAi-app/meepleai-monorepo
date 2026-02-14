using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using MediatR;
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
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
