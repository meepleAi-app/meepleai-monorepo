using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for Verify2FAQuery.
/// DDD: Thin wrapper - delegates to ITotpService for verification logic.
/// </summary>
internal class Verify2FAQueryHandler : IQueryHandler<Verify2FAQuery, Verify2FAResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly ILogger<Verify2FAQueryHandler> _logger;

    public Verify2FAQueryHandler(
        IUserRepository userRepository,
        ITotpService totpService,
        ILogger<Verify2FAQueryHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Verify2FAResult> Handle(Verify2FAQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            // Get user by email
            var email = new Domain.ValueObjects.Email(query.Email);
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning("2FA verify failed: User not found for email {Email}", query.Email);
                return new Verify2FAResult(IsValid: false, ErrorMessage: "User not found");
            }

            // Verify using TotpService (handles both TOTP and backup codes)
            bool isValid;
            if (query.IsBackupCode)
            {
                isValid = await _totpService.VerifyBackupCodeAsync(user.Id, query.Code, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                isValid = await _totpService.VerifyCodeAsync(user.Id, query.Code, cancellationToken).ConfigureAwait(false);
            }

            return new Verify2FAResult(IsValid: isValid);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: QUERY HANDLER PATTERN - CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns Result pattern.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying 2FA code for email {Email}", query.Email);
            return new Verify2FAResult(IsValid: false, ErrorMessage: "Verification error");
        }
#pragma warning restore CA1031
    }
}
