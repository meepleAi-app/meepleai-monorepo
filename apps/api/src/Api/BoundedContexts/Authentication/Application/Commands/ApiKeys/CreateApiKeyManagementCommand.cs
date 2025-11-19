using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to create a new API key for a user.
/// </summary>
public record CreateApiKeyManagementCommand(
    string UserId,
    CreateApiKeyRequest Request
) : ICommand<CreateApiKeyResponse>;
