using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles API key logout acknowledgement (legacy cookie cleanup happens at the endpoint level).
/// </summary>
public class LogoutApiKeyCommandHandler : ICommandHandler<LogoutApiKeyCommand, LogoutApiKeyResponse>
{
    public Task<LogoutApiKeyResponse> Handle(LogoutApiKeyCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(new LogoutApiKeyResponse(
            Message: "API key logout acknowledged."
        ));
    }
}
