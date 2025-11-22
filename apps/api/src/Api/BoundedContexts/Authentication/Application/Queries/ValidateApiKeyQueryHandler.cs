using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles API key validation and returns user information if valid.
/// </summary>
public class ValidateApiKeyQueryHandler : IQueryHandler<ValidateApiKeyQuery, UserDto?>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IUserRepository _userRepository;

    public ValidateApiKeyQueryHandler(
        IApiKeyRepository apiKeyRepository,
        IUserRepository userRepository)
    {
        _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UserDto?> Handle(ValidateApiKeyQuery query, CancellationToken cancellationToken)
    {
        // Extract key prefix (first 8 characters)
        if (query.PlaintextKey.Length < 8)
            return null;

        var keyPrefix = query.PlaintextKey[..8];

        // Find API key by prefix
        var apiKey = await _apiKeyRepository.GetByKeyPrefixAsync(keyPrefix, cancellationToken);

        if (apiKey == null)
            return null;

        // Verify the full key
        if (!apiKey.VerifyKey(query.PlaintextKey))
            return null;

        // Check if key is valid (active, not revoked, not expired)
        if (!apiKey.IsValidKey())
            return null;

        // Update last used timestamp
        apiKey.MarkAsUsed();
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken);

        // Get user information
        var user = await _userRepository.GetByIdAsync(apiKey.UserId, cancellationToken);

        if (user == null)
            return null;

        return MapToUserDto(user);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt
        );
    }
}