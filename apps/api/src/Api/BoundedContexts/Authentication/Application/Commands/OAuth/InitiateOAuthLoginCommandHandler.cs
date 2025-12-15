using System.Security.Cryptography;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Handles OAuth login initiation with CQRS pattern.
/// Business logic: CSRF state generation → State storage → Authorization URL generation.
/// Infrastructure delegation: Provider HTTP communication and state storage via IOAuthService.
/// </summary>
public sealed class InitiateOAuthLoginCommandHandler : ICommandHandler<InitiateOAuthLoginCommand, InitiateOAuthLoginResult>
{
    private readonly IOAuthService _oauthService;
    private readonly ILogger<InitiateOAuthLoginCommandHandler> _logger;

    public InitiateOAuthLoginCommandHandler(
        IOAuthService oauthService,
        ILogger<InitiateOAuthLoginCommandHandler> logger)
    {
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitiateOAuthLoginResult> Handle(InitiateOAuthLoginCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Validate provider (business logic)
            if (string.IsNullOrWhiteSpace(command.Provider))
            {
                _logger.LogWarning("OAuth login initiation failed: Provider not specified");
                return new InitiateOAuthLoginResult
                {
                    Success = false,
                    ErrorMessage = "OAuth provider must be specified"
                };
            }

            var provider = command.Provider.ToLowerInvariant();
            if (provider is not ("google" or "discord" or "github"))
            {
                _logger.LogWarning("OAuth login initiation failed: Unsupported provider {Provider}", command.Provider);
                return new InitiateOAuthLoginResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported OAuth provider: {command.Provider}"
                };
            }

            // Step 1: Generate secure CSRF state (business logic)
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            // Step 2: Store state with expiration (infrastructure - delegate to service)
            await _oauthService.StoreStateAsync(state).ConfigureAwait(false);

            // Step 3: Get authorization URL (infrastructure - delegate to service)
            var authUrl = await _oauthService.GetAuthorizationUrlAsync(provider, state).ConfigureAwait(false);

            _logger.LogInformation("OAuth login initiated for provider {Provider}, IP: {IpAddress}",
                provider,
                command.IpAddress ?? "unknown");

            return new InitiateOAuthLoginResult
            {
                Success = true,
                AuthorizationUrl = authUrl
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument during OAuth login initiation for provider {Provider}", command.Provider);
            return new InitiateOAuthLoginResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "OAuth provider {Provider} is not configured or unavailable", command.Provider);
            return new InitiateOAuthLoginResult
            {
                Success = false,
                ErrorMessage = $"OAuth provider {command.Provider} is not available. Please contact support."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OAuth login initiation for provider {Provider}", command.Provider);
            return new InitiateOAuthLoginResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while initiating OAuth login"
            };
        }
    }
}
