using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles API key validation and returns the associated user profile.
/// Browser clients must store API keys outside of cookies (e.g., secure storage) and send them via headers.
/// </summary>
internal class LoginWithApiKeyCommandHandler : ICommandHandler<LoginWithApiKeyCommand, ApiKeyLoginResponse>
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
        ArgumentNullException.ThrowIfNull(command);
        // Validate API key
        var validationResult = await _apiKeyService.ValidateApiKeyAsync(command.ApiKey, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.InvalidReason ?? "Invalid or expired API key");
        }

        // Get user information
        var userId = Guid.Parse(validationResult.UserId!);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);

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

        return new ApiKeyLoginResponse(
            User: userDto,
            ApiKeyId: validationResult.ApiKeyId!,
            Message: "API key authenticated successfully."
        );
    }
}
