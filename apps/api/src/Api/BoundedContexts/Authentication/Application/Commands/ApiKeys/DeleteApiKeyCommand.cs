using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to delete an API key permanently (admin only operation).
/// </summary>
public record DeleteApiKeyCommand(
    string KeyId,
    string UserId
) : ICommand<bool>;
