using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Handles linking an OAuth provider account to a user.
/// Validates user exists and prevents duplicate provider links.
/// </summary>
internal sealed class LinkOAuthAccountCommandHandler : ICommandHandler<LinkOAuthAccountCommand, LinkOAuthAccountResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LinkOAuthAccountCommandHandler> _logger;

    public LinkOAuthAccountCommandHandler(
        IUserRepository userRepository,
        IOAuthAccountRepository oauthAccountRepository,
        IUnitOfWork unitOfWork,
        ILogger<LinkOAuthAccountCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _oauthAccountRepository = oauthAccountRepository ?? throw new ArgumentNullException(nameof(oauthAccountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LinkOAuthAccountResult> Handle(LinkOAuthAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Load user by ID
            var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for OAuth linking", command.UserId);
                return new LinkOAuthAccountResult
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            // Check if provider already linked
            var existingAccount = await _oauthAccountRepository.GetByUserIdAndProviderAsync(
                command.UserId,
                command.Provider,
                cancellationToken).ConfigureAwait(false);

            if (existingAccount != null)
            {
                _logger.LogWarning("User {UserId} already has {Provider} OAuth account linked", command.UserId, command.Provider);
                return new LinkOAuthAccountResult
                {
                    Success = false,
                    ErrorMessage = $"OAuth provider '{command.Provider}' is already linked to this user"
                };
            }

            // Create OAuth account domain entity
            var oauthAccountId = Guid.NewGuid();
            var oauthAccount = new OAuthAccount(
                id: oauthAccountId,
                userId: command.UserId,
                provider: command.Provider,
                providerUserId: command.ProviderUserId,
                accessTokenEncrypted: command.AccessTokenEncrypted,
                refreshTokenEncrypted: command.RefreshTokenEncrypted,
                tokenExpiresAt: command.TokenExpiresAt);

            // Call domain validation
            user.LinkOAuthAccount(oauthAccount);

            // Save via repository
            await _oauthAccountRepository.AddAsync(oauthAccount, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully linked {Provider} OAuth account for user {UserId}", command.Provider, command.UserId);

            return new LinkOAuthAccountResult
            {
                OAuthAccountId = oauthAccountId,
                Success = true
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Validation failed while linking OAuth account for user {UserId}", command.UserId);
            return new LinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (DomainException ex)
        {
            _logger.LogError(ex, "Domain validation failed while linking OAuth account for user {UserId}", command.UserId);
            return new LinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while linking OAuth account for user {UserId}", command.UserId);
            return new LinkOAuthAccountResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while linking the OAuth account"
            };
        }
    }
}
