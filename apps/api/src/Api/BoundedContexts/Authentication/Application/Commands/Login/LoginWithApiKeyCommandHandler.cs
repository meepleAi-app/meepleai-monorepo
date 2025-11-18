using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles API key login and sets secure httpOnly cookie for browser-based API key authentication.
/// This allows users to securely authenticate with API keys without storing them in localStorage.
/// </summary>
public class LoginWithApiKeyCommandHandler : ICommandHandler<LoginWithApiKeyCommand, ApiKeyLoginResponse>
{
    private readonly ApiKeyAuthenticationService _apiKeyService;
    private readonly IUserRepository _userRepository;

    public LoginWithApiKeyCommandHandler(
        ApiKeyAuthenticationService apiKeyService,
        IUserRepository userRepository)
    {
        _apiKeyService = apiKeyService;
        _userRepository = userRepository;
    }

    public async Task<ApiKeyLoginResponse> Handle(LoginWithApiKeyCommand command, CancellationToken cancellationToken)
    {
        // Validate API key
        var validationResult = await _apiKeyService.ValidateApiKeyAsync(command.ApiKey, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.InvalidReason ?? "Invalid or expired API key");
        }

        // Get user information
        var userId = Guid.Parse(validationResult.UserId!);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("User not found");
        }

        // Map to DTO
        var userDto = new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt
        );

        // The actual cookie setting happens in the endpoint using CookieHelpers.WriteApiKeyCookie()
        // We return the validation result here
        return new ApiKeyLoginResponse(
            User: userDto,
            ApiKeyId: validationResult.ApiKeyId!,
            Message: "API key authenticated successfully. Cookie has been set."
        );
    }
}
