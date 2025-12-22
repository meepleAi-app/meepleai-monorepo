using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for Get2FAStatusQuery.
/// DDD: Thin wrapper - delegates to ITotpService for status retrieval.
/// </summary>
internal class Get2FAStatusQueryHandler : IQueryHandler<Get2FAStatusQuery, TwoFactorStatusDto?>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<Get2FAStatusQueryHandler> _logger;

    public Get2FAStatusQueryHandler(
        ITotpService totpService,
        ILogger<Get2FAStatusQueryHandler> logger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TwoFactorStatusDto?> Handle(Get2FAStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            var status = await _totpService.GetTwoFactorStatusAsync(query.UserId, cancellationToken).ConfigureAwait(false);

            // Map from infrastructure response to application DTO
            return new TwoFactorStatusDto
            {
                IsEnabled = status.IsEnabled,
                EnabledAt = status.EnabledAt,
                UnusedBackupCodesCount = status.UnusedBackupCodesCount
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: QUERY HANDLER PATTERN - CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns null on failure.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting 2FA status for user {UserId}", query.UserId);
            return null;
        }
#pragma warning restore CA1031
    }
}
