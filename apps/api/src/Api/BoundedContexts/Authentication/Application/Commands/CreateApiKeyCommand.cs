using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to create a new API key for a user.
/// </summary>
public record CreateApiKeyCommand(
    Guid UserId,
    string KeyName,
    string Scopes,
    DateTime? ExpiresAt = null,
    string? Metadata = null
) : ICommand<CreateApiKeyResponse>;
