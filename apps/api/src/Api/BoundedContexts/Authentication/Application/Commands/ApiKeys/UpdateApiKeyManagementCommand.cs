using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to update an existing API key.
/// </summary>
public record UpdateApiKeyManagementCommand(
    string KeyId,
    string UserId,
    UpdateApiKeyRequest Request
) : ICommand<ApiKeyDto?>;
