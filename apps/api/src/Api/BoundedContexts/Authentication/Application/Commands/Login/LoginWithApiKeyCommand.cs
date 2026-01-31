using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to validate an API key and return the associated profile.
/// Clients are responsible for storing the API key securely (e.g., Authorization header).
/// </summary>
internal record LoginWithApiKeyCommand(
    string ApiKey
) : ICommand<ApiKeyLoginResponse>;

/// <summary>
/// Response for API key login containing user information.
/// The API key is not returned in responses. Clients should persist it on their side.
/// </summary>
internal record ApiKeyLoginResponse(
    UserDto User,
    string ApiKeyId,
    string Message
);
