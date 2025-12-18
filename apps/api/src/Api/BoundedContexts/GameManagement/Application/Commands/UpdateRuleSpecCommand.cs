using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update a rule specification for a game.
/// Creates a new version of the RuleSpec.
/// Issue #2055: Supports optimistic concurrency via ExpectedETag.
/// </summary>
internal record UpdateRuleSpecCommand(
    Guid GameId,
    string? Version,
    IReadOnlyList<RuleAtomDto> Atoms,
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null,
    /// <summary>
    /// Issue #2055: Optional ETag for optimistic concurrency.
    /// If provided, will verify the current latest version matches before creating new version.
    /// </summary>
    string? ExpectedETag = null,
    /// <summary>
    /// Issue #2055: Optional parent version ID for branching.
    /// If provided, sets this version as parent of the new version.
    /// </summary>
    Guid? ParentVersionId = null
) : ICommand<RuleSpecDto>;
