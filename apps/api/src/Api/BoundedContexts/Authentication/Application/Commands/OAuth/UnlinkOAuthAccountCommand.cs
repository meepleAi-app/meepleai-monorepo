using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Command to unlink an OAuth provider account from a user.
/// Enforces business rule: Cannot unlink if it's the only authentication method (prevents lockout).
/// </summary>
internal sealed record UnlinkOAuthAccountCommand : ICommand<UnlinkOAuthAccountResult>
{
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
}

/// <summary>
/// Result of OAuth account unlinking operation.
/// </summary>
internal sealed record UnlinkOAuthAccountResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
