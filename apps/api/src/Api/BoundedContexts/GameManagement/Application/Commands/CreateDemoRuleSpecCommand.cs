using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a demo RuleSpec for a game (admin/testing purposes).
/// If a RuleSpec already exists, returns the existing one.
/// </summary>
public record CreateDemoRuleSpecCommand(
    Guid GameId
) : ICommand<RuleSpecDto>;
