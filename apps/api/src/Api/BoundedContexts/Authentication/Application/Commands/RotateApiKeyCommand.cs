using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to rotate an API key by creating a new key and revoking the old one.
/// </summary>
public record RotateApiKeyCommand(
    string KeyId,
    string UserId,
    RotateApiKeyRequest Request
) : ICommand<RotateApiKeyResponse?>;
