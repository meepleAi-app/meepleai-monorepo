using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for Get2FAStatusQuery.
/// DDD: Thin wrapper - delegates to ITotpService for status retrieval.
/// </summary>
public class Get2FAStatusQueryHandler : IQueryHandler<Get2FAStatusQuery, TwoFactorStatusDto?>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<Get2FAStatusQueryHandler> _logger;

    public Get2FAStatusQueryHandler(
        ITotpService totpService,
        ILogger<Get2FAStatusQueryHandler> logger)
    {
        _totpService = totpService;
        _logger = logger;
    }

    public async Task<TwoFactorStatusDto?> Handle(Get2FAStatusQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _totpService.GetTwoFactorStatusAsync(query.UserId);

            // Map from infrastructure response to application DTO
            return new TwoFactorStatusDto
            {
                IsEnabled = status.IsEnabled,
                EnabledAt = status.EnabledAt,
                UnusedBackupCodesCount = status.UnusedBackupCodesCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting 2FA status for user {UserId}", query.UserId);
            return null;
        }
    }
}
