using Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

/// <summary>
/// Adds a new email to the staging allowlist. Idempotency: throws
/// <see cref="Api.Middleware.Exceptions.ConflictException"/> (HTTP 409) on duplicate.
/// </summary>
/// <param name="Email">Raw email; normalized to lowercase by the handler.</param>
/// <param name="Note">Optional context (max 500 chars).</param>
/// <param name="AddedByUserId">User performing the add (superadmin); <c>null</c> only for system seeds.</param>
public sealed record AddStagingAllowlistEntryCommand(
    string Email,
    string? Note,
    Guid? AddedByUserId) : IRequest<StagingAllowlistEntryDto>;
