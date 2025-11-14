using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for Verify2FAQuery.
/// DDD: Thin wrapper - delegates to ITotpService for verification logic.
/// </summary>
public class Verify2FAQueryHandler : IQueryHandler<Verify2FAQuery, Verify2FAResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly ILogger<Verify2FAQueryHandler> _logger;

    public Verify2FAQueryHandler(
        IUserRepository userRepository,
        ITotpService totpService,
        ILogger<Verify2FAQueryHandler> logger)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _logger = logger;
    }

    public async Task<Verify2FAResult> Handle(Verify2FAQuery query, CancellationToken cancellationToken)
    {
        try
        {
            // Get user by email
            var email = new Domain.ValueObjects.Email(query.Email);
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("2FA verify failed: User not found for email {Email}", query.Email);
                return new Verify2FAResult(IsValid: false, ErrorMessage: "User not found");
            }

            // Verify using TotpService (handles both TOTP and backup codes)
            bool isValid;
            if (query.IsBackupCode)
            {
                isValid = await _totpService.VerifyBackupCodeAsync(user.Id, query.Code);
            }
            else
            {
                isValid = await _totpService.VerifyCodeAsync(user.Id, query.Code);
            }

            return new Verify2FAResult(IsValid: isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying 2FA code for email {Email}", query.Email);
            return new Verify2FAResult(IsValid: false, ErrorMessage: "Verification error");
        }
    }
}
