using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to acknowledge API key logout (clients clear their stored keys).
/// </summary>
public record LogoutApiKeyCommand : ICommand<LogoutApiKeyResponse>;

/// <summary>
/// Response for API key logout.
/// </summary>
public record LogoutApiKeyResponse(
    string Message
);
