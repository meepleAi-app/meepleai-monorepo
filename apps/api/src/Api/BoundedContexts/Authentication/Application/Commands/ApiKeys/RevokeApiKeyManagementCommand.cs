using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke an API key.
/// </summary>
public record RevokeApiKeyManagementCommand(
    string KeyId,
    string UserId
) : ICommand<bool>;
