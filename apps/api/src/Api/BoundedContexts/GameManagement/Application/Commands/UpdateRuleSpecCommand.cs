using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update a rule specification for a game.
/// Creates a new version of the RuleSpec.
/// </summary>
public record UpdateRuleSpecCommand(
    Guid GameId,
    string? Version,
    IReadOnlyList<RuleAtomDto> Atoms,
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<RuleSpecDto>;
