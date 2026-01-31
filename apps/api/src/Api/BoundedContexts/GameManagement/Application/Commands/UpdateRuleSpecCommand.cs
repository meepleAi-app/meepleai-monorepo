using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update a rule specification for a game.
/// Creates a new version of the RuleSpec.
/// Issue #2055: Supports optimistic concurrency via ExpectedETag.
/// </summary>
/// <param name="GameId">ID of the game to update.</param>
/// <param name="Version">Version string for the rule specification.</param>
/// <param name="Atoms">List of rule atoms defining the specification.</param>
/// <param name="UserId">ID of the user making the update.</param>
/// <param name="IpAddress">Optional IP address of the requester.</param>
/// <param name="UserAgent">Optional user agent of the requester.</param>
/// <param name="ExpectedETag">Issue #2055: Optional ETag for optimistic concurrency. If provided, will verify the current latest version matches before creating new version.</param>
/// <param name="ParentVersionId">Issue #2055: Optional parent version ID for branching. If provided, sets this version as parent of the new version.</param>
internal record UpdateRuleSpecCommand(
    Guid GameId,
    string? Version,
    IReadOnlyList<RuleAtomDto> Atoms,
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null,
    string? ExpectedETag = null,
    Guid? ParentVersionId = null
) : ICommand<RuleSpecDto>;
