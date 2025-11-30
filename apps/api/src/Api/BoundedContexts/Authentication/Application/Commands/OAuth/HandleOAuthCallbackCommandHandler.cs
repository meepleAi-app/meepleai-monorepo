using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Handles OAuth callback processing with full CQRS implementation.
/// Business logic: CSRF validation → Token exchange → User creation/linking → Token encryption → Session creation.
/// Infrastructure delegation: Provider HTTP communication via IOAuthService.
/// </summary>
public sealed class HandleOAuthCallbackCommandHandler : ICommandHandler<HandleOAuthCallbackCommand, HandleOAuthCallbackResult>
{
    private const string EncryptionPurpose = "OAuthTokens";

    private readonly IOAuthService _oauthService;
    private readonly IUserRepository _userRepository;
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<HandleOAuthCallbackCommandHandler> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly TimeProvider _timeProvider;
    private readonly MeepleAiDbContext _db;

    public HandleOAuthCallbackCommandHandler(
        IOAuthService oauthService,
        IUserRepository userRepository,
        IOAuthAccountRepository oauthAccountRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<HandleOAuthCallbackCommandHandler> logger,
        IEncryptionService encryptionService,
        TimeProvider timeProvider,
        MeepleAiDbContext db)
    {
        _oauthService = oauthService;
        _userRepository = userRepository;
        _oauthAccountRepository = oauthAccountRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
        _logger = logger;
        _encryptionService = encryptionService;
        _timeProvider = timeProvider;
        _db = db;
    }

    public async Task<HandleOAuthCallbackResult> Handle(HandleOAuthCallbackCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate CSRF state token (infrastructure concern - delegate to service)
            var isStateValid = await _oauthService.ValidateStateAsync(command.State).ConfigureAwait(false);
            if (!isStateValid)
            {
                _logger.LogWarning("Invalid OAuth state parameter for provider {Provider}", command.Provider);
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "Invalid state parameter. Possible CSRF attack."
                };
            }

            // Step 2: Exchange authorization code for access token (infrastructure - delegate to service)
            OAuthTokenResponse tokenResponse;
            try
            {
                tokenResponse = await _oauthService.ExchangeCodeForTokenAsync(command.Provider, command.Code).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to exchange OAuth code for token. Provider: {Provider}", command.Provider);
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "OAuth token exchange failed. Please try again."
                };
            }

            // Step 3: Get user info from provider (infrastructure - delegate to service)
            OAuthUserInfo userInfo;
            try
            {
                userInfo = await _oauthService.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to get user info from OAuth provider. Provider: {Provider}", command.Provider);
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "Failed to get user information from OAuth provider. Please try again."
                };
            }

            // Validate user info contains required email (AUTH-06 requirement)
            if (string.IsNullOrEmpty(userInfo.Email))
            {
                _logger.LogError("OAuth provider {Provider} did not return email address", command.Provider);
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "OAuth provider did not provide email address. Please check your account settings."
                };
            }

            // Step 4: Find existing OAuth account to determine flow (business logic in handler)
            var oauthAccount = await _db.OAuthAccounts
                .Include(oa => oa.User)
                .FirstOrDefaultAsync(oa =>
                    oa.Provider == command.Provider.ToLowerInvariant() &&
                    oa.ProviderUserId == userInfo.Id, cancellationToken);

            UserEntity? user;
            bool isNewUser = false;

            if (oauthAccount != null)
            {
                // Existing OAuth account - update token (business logic in handler)
                if (oauthAccount.User == null)
                {
                    _logger.LogError("OAuth account found but user entity not loaded. Provider: {Provider}", command.Provider);
                    return new HandleOAuthCallbackResult
                    {
                        Success = false,
                        ErrorMessage = "Account data inconsistency detected. Please contact support."
                    };
                }

                user = oauthAccount.User;
                await UpdateOAuthTokenAsync(oauthAccount, tokenResponse, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("OAuth login for existing account. Provider: {Provider}, UserId: {UserId}", command.Provider, user.Id);
            }
            else
            {
                // Check if user exists with same email (auto-link for MVP)
                user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);

                if (user == null)
                {
                    // Create new user (business logic in handler)
                    // CWE-476: Safe email split with null/empty guards
                    var emailParts = userInfo.Email?.Split('@') ?? Array.Empty<string>();
                    var emailPrefix = emailParts.Length > 0 && !string.IsNullOrEmpty(emailParts[0])
                        ? emailParts[0]
                        : "User";

                    user = new UserEntity
                    {
                        Id = Guid.NewGuid(),
                        // CS8602: False positive - Email validated non-null above
#pragma warning disable CS8602
                        Email = userInfo.Email.ToLowerInvariant(),
#pragma warning restore CS8602
                        DisplayName = userInfo.Name ?? emailPrefix,
                        PasswordHash = GenerateRandomPasswordHash(), // No password for OAuth-only users
                        Role = UserRole.User.ToString(),
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                    };
                    _db.Users.Add(user);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    isNewUser = true;
                    _logger.LogInformation("Created new user via OAuth. Provider: {Provider}, UserId: {UserId}", command.Provider, user.Id);
                }
                else
                {
                    _logger.LogInformation("Linking OAuth to existing user. Provider: {Provider}, UserId: {UserId}", command.Provider, user.Id);
                }

                // Create OAuth account link (business logic in handler)
                await CreateOAuthAccountAsync(user.Id, command.Provider, userInfo, tokenResponse, cancellationToken).ConfigureAwait(false);
            }

            // Step 5: Create session via MediatR (already CQRS-compliant)
            var createSessionCommand = new CreateSessionCommand(
                UserId: user.Id,
                IpAddress: command.IpAddress,
                UserAgent: command.UserAgent);

            var sessionResponse = await _mediator.Send(createSessionCommand, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "OAuth callback successful for provider {Provider}, UserId: {UserId}, IsNewUser: {IsNewUser}",
                command.Provider,
                user.Id,
                isNewUser);

            return new HandleOAuthCallbackResult
            {
                UserId = user.Id,
                IsNewUser = isNewUser,
                Success = true,
                SessionToken = sessionResponse.SessionToken
            };
        }
        catch (SharedKernel.Domain.Exceptions.ValidationException ex)
        {
            _logger.LogError(ex, "Validation failed during OAuth callback for provider {Provider}", command.Provider);
            return new HandleOAuthCallbackResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (DomainException ex)
        {
            _logger.LogError(ex, "Domain validation failed during OAuth callback for provider {Provider}", command.Provider);
            return new HandleOAuthCallbackResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OAuth callback for provider {Provider}", command.Provider);
            return new HandleOAuthCallbackResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred during OAuth authentication"
            };
        }
    }

    /// <summary>
    /// Creates OAuth account link with encrypted tokens (business logic - moved from OAuthService)
    /// </summary>
    private async Task CreateOAuthAccountAsync(Guid userId,
        string provider,
        OAuthUserInfo userInfo,
        OAuthTokenResponse tokenResponse,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = tokenResponse.ExpiresIn.HasValue
            ? now.AddSeconds(tokenResponse.ExpiresIn.Value)
            : (DateTime?)null;

        var accessTokenEncrypted = await _encryptionService.EncryptAsync(tokenResponse.AccessToken, EncryptionPurpose).ConfigureAwait(false);
        var refreshTokenEncrypted = tokenResponse.RefreshToken != null
            ? await _encryptionService.EncryptAsync(tokenResponse.RefreshToken, EncryptionPurpose)
            : null;

        var oauthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider.ToLowerInvariant(),
            ProviderUserId = userInfo.Id,
            AccessTokenEncrypted = accessTokenEncrypted,
            RefreshTokenEncrypted = refreshTokenEncrypted,
            TokenExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now,
            User = null! // Will be loaded by EF
        };

        _db.OAuthAccounts.Add(oauthAccount);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates OAuth account tokens with encryption (business logic - moved from OAuthService)
    /// </summary>
    private async Task UpdateOAuthTokenAsync(OAuthAccountEntity account, OAuthTokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = tokenResponse.ExpiresIn.HasValue
            ? now.AddSeconds(tokenResponse.ExpiresIn.Value)
            : (DateTime?)null;

        account.AccessTokenEncrypted = await _encryptionService.EncryptAsync(tokenResponse.AccessToken, EncryptionPurpose).ConfigureAwait(false);

        if (tokenResponse.RefreshToken != null)
        {
            account.RefreshTokenEncrypted = await _encryptionService.EncryptAsync(tokenResponse.RefreshToken, EncryptionPurpose).ConfigureAwait(false);
        }

        account.TokenExpiresAt = expiresAt;
        account.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates random password hash for OAuth-only users (business logic - moved from OAuthService)
    /// </summary>
    private static string GenerateRandomPasswordHash()
    {
        // For OAuth-only users, generate a random unguessable password hash
        // They won't know the password and can only login via OAuth
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }
}
