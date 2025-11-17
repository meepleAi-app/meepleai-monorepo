using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get the current session status including expiry and last seen time.
/// DDD CQRS: Query for read-only operation.
/// AUTH-05: Session management
/// </summary>
public sealed record GetSessionStatusQuery(
    string TokenHash,
    int InactivityTimeoutDays
) : IQuery<SessionStatusResponse?>;
