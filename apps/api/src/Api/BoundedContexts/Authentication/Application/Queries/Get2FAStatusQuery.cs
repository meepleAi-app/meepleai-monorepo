using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get the current 2FA status for a user.
/// DDD CQRS: Query for read-only operation.
/// </summary>
public sealed record Get2FAStatusQuery(
    Guid UserId
) : IQuery<TwoFactorStatusDto?>;
