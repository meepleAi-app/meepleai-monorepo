using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles API key logout by clearing the httpOnly cookie.
/// The actual cookie removal happens in the endpoint using CookieHelpers.RemoveApiKeyCookie().
/// </summary>
public class LogoutApiKeyCommandHandler : ICommandHandler<LogoutApiKeyCommand, LogoutApiKeyResponse>
{
    public Task<LogoutApiKeyResponse> Handle(LogoutApiKeyCommand command, CancellationToken cancellationToken)
    {
        // The actual cookie removal happens in the endpoint
        return Task.FromResult(new LogoutApiKeyResponse(
            Message: "API key cookie has been removed."
        ));
    }
}
