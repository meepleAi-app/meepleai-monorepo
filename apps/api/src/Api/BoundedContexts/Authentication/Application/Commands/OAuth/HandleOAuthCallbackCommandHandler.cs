using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Handles OAuth callback processing.
/// Orchestrates: CSRF validation → Token exchange → User creation/linking → Session creation.
/// Delegates provider communication to IOAuthService (legacy).
/// </summary>
public sealed class HandleOAuthCallbackCommandHandler : ICommandHandler<HandleOAuthCallbackCommand, HandleOAuthCallbackResult>
{
    private readonly IOAuthService _oauthService;
    private readonly IUserRepository _userRepository;
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<HandleOAuthCallbackCommandHandler> _logger;
    private readonly IEncryptionService _encryptionService;

    public HandleOAuthCallbackCommandHandler(
        IOAuthService oauthService,
        IUserRepository userRepository,
        IOAuthAccountRepository oauthAccountRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<HandleOAuthCallbackCommandHandler> logger,
        IEncryptionService encryptionService)
    {
        _oauthService = oauthService;
        _userRepository = userRepository;
        _oauthAccountRepository = oauthAccountRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task<HandleOAuthCallbackResult> Handle(HandleOAuthCallbackCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate CSRF state token
            var isStateValid = await _oauthService.ValidateStateAsync(command.State);
            if (!isStateValid)
            {
                _logger.LogWarning("Invalid OAuth state parameter for provider {Provider}", command.Provider);
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "Invalid state parameter. Possible CSRF attack."
                };
            }

            // Step 2: Use legacy OAuthService to exchange code for tokens and get user info
            // This delegates the provider-specific HTTP communication to the existing service
            OAuthCallbackResult callbackResult;
            try
            {
                callbackResult = await _oauthService.HandleCallbackAsync(
                    command.Provider,
                    command.Code,
                    command.State);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "OAuth callback failed: Unauthorized");
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "OAuth callback failed: Invalid operation");
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }

            // Step 3: Parse user ID from callback result
            if (!Guid.TryParse(callbackResult.User.Id, out var userId))
            {
                _logger.LogError("Failed to parse user ID from OAuth callback result");
                return new HandleOAuthCallbackResult
                {
                    Success = false,
                    ErrorMessage = "Failed to process OAuth callback"
                };
            }

            // Step 4: Create session via MediatR
            var createSessionCommand = new CreateSessionCommand(
                UserId: userId,
                IpAddress: command.IpAddress,
                UserAgent: command.UserAgent);

            var sessionResponse = await _mediator.Send(createSessionCommand, cancellationToken);

            _logger.LogInformation(
                "OAuth callback successful for provider {Provider}, UserId: {UserId}, IsNewUser: {IsNewUser}",
                command.Provider,
                userId,
                callbackResult.IsNewUser);

            return new HandleOAuthCallbackResult
            {
                UserId = userId,
                IsNewUser = callbackResult.IsNewUser,
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
}
