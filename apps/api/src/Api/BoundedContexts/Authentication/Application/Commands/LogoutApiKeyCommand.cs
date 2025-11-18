using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to logout API key authentication by removing the httpOnly cookie.
/// </summary>
public record LogoutApiKeyCommand : ICommand<LogoutApiKeyResponse>;

/// <summary>
/// Response for API key logout.
/// </summary>
public record LogoutApiKeyResponse(
    string Message
);
