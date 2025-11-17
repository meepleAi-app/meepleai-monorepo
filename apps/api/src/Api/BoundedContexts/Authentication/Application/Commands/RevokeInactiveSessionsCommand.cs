using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to auto-revoke sessions that have been inactive for longer than the configured timeout.
/// Returns the number of sessions revoked.
/// </summary>
public record RevokeInactiveSessionsCommand : ICommand<int>;
