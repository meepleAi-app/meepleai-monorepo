using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Queries.PasswordReset;

/// <summary>
/// Handles password reset token validation with CQRS pattern.
/// Business logic: Token format validation → Token existence check → Expiry check.
/// Infrastructure delegation: Database access via password reset service.
/// </summary>
public sealed class ValidatePasswordResetTokenQueryHandler : IQueryHandler<ValidatePasswordResetTokenQuery, ValidatePasswordResetTokenResult>
{
    private readonly IPasswordResetService _passwordResetService;
    private readonly ILogger<ValidatePasswordResetTokenQueryHandler> _logger;

    public ValidatePasswordResetTokenQueryHandler(
        IPasswordResetService passwordResetService,
        ILogger<ValidatePasswordResetTokenQueryHandler> logger)
    {
        _passwordResetService = passwordResetService;
        _logger = logger;
    }

    public async Task<ValidatePasswordResetTokenResult> Handle(ValidatePasswordResetTokenQuery query, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token format (basic business logic)
            if (string.IsNullOrWhiteSpace(query.Token))
            {
                return new ValidatePasswordResetTokenResult { IsValid = false };
            }

            // Delegate to infrastructure service for token validation
            var isValid = await _passwordResetService.ValidateResetTokenAsync(query.Token, cancellationToken);

            return new ValidatePasswordResetTokenResult { IsValid = isValid };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating password reset token");
            return new ValidatePasswordResetTokenResult { IsValid = false };
        }
    }
}
