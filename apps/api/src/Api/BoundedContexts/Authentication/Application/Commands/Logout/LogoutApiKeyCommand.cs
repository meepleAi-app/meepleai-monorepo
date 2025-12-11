using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
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
