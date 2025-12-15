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
internal sealed class HandleOAuthCallbackCommandHandler : ICommandHandler<HandleOAuthCallbackCommand, HandleOAuthCallbackResult>
{
    private const string EncryptionPurpose = "OAuthTokens";

    private readonly IOAuthService _oauthService;
    private readonly IMediator _mediator;
    private readonly ILogger<HandleOAuthCallbackCommandHandler> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly TimeProvider _timeProvider;
    private readonly MeepleAiDbContext _db;

    public HandleOAuthCallbackCommandHandler(
        IOAuthService oauthService,
        IMediator mediator,
        ILogger<HandleOAuthCallbackCommandHandler> logger,
        IEncryptionService encryptionService,
        TimeProvider timeProvider,
        MeepleAiDbContext db)
    {
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<HandleOAuthCallbackResult> Handle(HandleOAuthCallbackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Step 1: Validate state and exchange code for user info
            var (tokenSuccess, userInfo, tokenResponse) = await ValidateAndExchangeTokenAsync(
                command.Provider, command.State, command.Code).ConfigureAwait(false);
            if (!tokenSuccess)
            {
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = userInfo as string ?? "OAuth authentication failed"
                };
            }

            // Step 2: Find or create user and link OAuth account
            var (user, isNewUser) = await FindOrCreateUserAsync(
                command.Provider, userInfo as OAuthUserInfo, tokenResponse!, cancellationToken).ConfigureAwait(false);

            // Step 3: Create session for authenticated user
            var sessionToken = await CreateSessionForUserAsync(
                user.Id, command.IpAddress, command.UserAgent, cancellationToken).ConfigureAwait(false);

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
                SessionToken = sessionToken
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
    /// Validates state token, exchanges code for access token, and gets user info.
    /// Returns (success, userInfo or errorMessage, tokenResponse).
    /// </summary>
    private async Task<(bool success, object userInfoOrError, OAuthTokenResponse? tokenResponse)> ValidateAndExchangeTokenAsync(
        string provider,
        string state,
        string code
                )
    {
        // Validate CSRF state token
        var isStateValid = await _oauthService.ValidateStateAsync(state).ConfigureAwait(false);
        if (!isStateValid)
        {
            _logger.LogWarning("Invalid OAuth state parameter for provider {Provider}", provider);
            return (false, "Invalid state parameter. Possible CSRF attack.", null);
        }

        // Exchange authorization code for access token
        OAuthTokenResponse tokenResponse;
        try
        {
            tokenResponse = await _oauthService.ExchangeCodeForTokenAsync(provider, code).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to exchange OAuth code for token. Provider: {Provider}", provider);
            return (false, "OAuth token exchange failed. Please try again.", null);
        }

        // Get user info from provider
        OAuthUserInfo userInfo;
        try
        {
            userInfo = await _oauthService.GetUserInfoAsync(provider, tokenResponse.AccessToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get user info from OAuth provider. Provider: {Provider}", provider);
            return (false, "Failed to get user information from OAuth provider. Please try again.", null);
        }

        // Validate user info contains required email
        if (string.IsNullOrEmpty(userInfo.Email))
        {
            _logger.LogError("OAuth provider {Provider} did not return email address", provider);
            return (false, "OAuth provider did not provide email address. Please check your account settings.", null);
        }

        return (true, userInfo, tokenResponse);
    }

    /// <summary>
    /// Finds existing user or creates new user, and links OAuth account.
    /// Returns (user, isNewUser).
    /// </summary>
    private async Task<(UserEntity user, bool isNewUser)> FindOrCreateUserAsync(
        string provider,
        OAuthUserInfo? userInfo,
        OAuthTokenResponse tokenResponse,
        CancellationToken cancellationToken)
    {
        // Find existing OAuth account
        var oauthAccount = await _db.OAuthAccounts
            .Include(oa => oa.User)
            .FirstOrDefaultAsync(oa =>
                oa.Provider == provider.ToLowerInvariant() &&
                oa.ProviderUserId == userInfo!.Id, cancellationToken).ConfigureAwait(false);

        UserEntity? user;
        bool isNewUser = false;

        if (oauthAccount != null)
        {
            // Existing OAuth account - update token
            if (oauthAccount.User == null)
            {
                throw new DomainException("Account data inconsistency detected. Please contact support.");
            }

            user = oauthAccount.User;
            await UpdateOAuthTokenAsync(oauthAccount, tokenResponse, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("OAuth login for existing account. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
        }
        else
        {
            // Check if user exists with same email (auto-link for MVP)
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userInfo!.Email.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                // Create new user
                var emailParts = userInfo!.Email?.Split('@') ?? Array.Empty<string>();
                var emailPrefix = emailParts.Length > 0 && !string.IsNullOrEmpty(emailParts[0])
                    ? emailParts[0]
                    : "User";

                user = new UserEntity
                {
                    Id = Guid.NewGuid(),
#pragma warning disable CS8602
                    Email = userInfo.Email.ToLowerInvariant(),
#pragma warning restore CS8602
                    DisplayName = userInfo.Name ?? emailPrefix,
                    PasswordHash = GenerateRandomPasswordHash(),
                    Role = UserRole.User.ToString(),
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                isNewUser = true;
                _logger.LogInformation("Created new user via OAuth. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
            }
            else
            {
                _logger.LogInformation("Linking OAuth to existing user. Provider: {Provider}, UserId: {UserId}", provider, user.Id);
            }

            // Create OAuth account link
            await CreateOAuthAccountAsync(user.Id, provider, userInfo!, tokenResponse, cancellationToken).ConfigureAwait(false);
        }

        return (user, isNewUser);
    }

    /// <summary>
    /// Creates session for authenticated user via MediatR.
    /// </summary>
    private async Task<string> CreateSessionForUserAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var createSessionCommand = new CreateSessionCommand(
            UserId: userId,
            IpAddress: ipAddress ?? "unknown",
            UserAgent: userAgent ?? "unknown");

        var sessionResponse = await _mediator.Send(createSessionCommand, cancellationToken).ConfigureAwait(false);

        return sessionResponse.SessionToken;
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
.ConfigureAwait(false) : null;

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
