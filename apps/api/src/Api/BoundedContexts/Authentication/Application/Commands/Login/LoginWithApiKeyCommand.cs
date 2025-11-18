using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to authenticate a user with API key and set httpOnly cookie.
/// This provides a secure way to use API keys in browser contexts.
/// </summary>
public record LoginWithApiKeyCommand(
    string ApiKey
) : ICommand<ApiKeyLoginResponse>;

/// <summary>
/// Response for API key login containing user information.
/// The API key is stored in a secure httpOnly cookie, not in the response body.
/// </summary>
public record ApiKeyLoginResponse(
    UserDto User,
    string ApiKeyId,
    string Message
);
