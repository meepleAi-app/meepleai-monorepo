using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Handles unlinking an OAuth provider account from a user.
/// Validates lockout prevention rules via domain logic.
/// </summary>
internal sealed class UnlinkOAuthAccountCommandHandler : ICommandHandler<UnlinkOAuthAccountCommand, UnlinkOAuthAccountResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnlinkOAuthAccountCommandHandler> _logger;

    public UnlinkOAuthAccountCommandHandler(
        IUserRepository userRepository,
        IOAuthAccountRepository oauthAccountRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnlinkOAuthAccountCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _oauthAccountRepository = oauthAccountRepository ?? throw new ArgumentNullException(nameof(oauthAccountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnlinkOAuthAccountResult> Handle(UnlinkOAuthAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Load user by ID
            var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for OAuth unlinking", command.UserId);
                return new UnlinkOAuthAccountResult
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            // Call domain validation (includes lockout prevention)
            user.UnlinkOAuthAccount(command.Provider);

            // Find OAuth account in repository
            var oauthAccount = await _oauthAccountRepository.GetByUserIdAndProviderAsync(
                command.UserId,
                command.Provider,
                cancellationToken).ConfigureAwait(false);

            if (oauthAccount == null)
            {
                // This shouldn't happen if domain logic is correct, but handle gracefully
                _logger.LogWarning("OAuth account not found for user {UserId} and provider {Provider}", command.UserId, command.Provider);
                return new UnlinkOAuthAccountResult
                {
                    Success = false,
                    ErrorMessage = $"OAuth provider '{command.Provider}' is not linked to this user"
                };
            }

            // Delete via repository
            await _oauthAccountRepository.DeleteAsync(oauthAccount, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully unlinked {Provider} OAuth account for user {UserId}", command.Provider, command.UserId);

            return new UnlinkOAuthAccountResult
            {
                Success = true
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Validation failed while unlinking OAuth account for user {UserId}", command.UserId);
            return new UnlinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (DomainException ex)
        {
            _logger.LogError(ex, "Domain validation failed while unlinking OAuth account for user {UserId}", command.UserId);
            return new UnlinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ValidationException, DomainException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result<T> pattern.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while unlinking OAuth account for user {UserId}", command.UserId);
            return new UnlinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while unlinking the OAuth account"
            };
        }
#pragma warning restore CA1031
    }
}
