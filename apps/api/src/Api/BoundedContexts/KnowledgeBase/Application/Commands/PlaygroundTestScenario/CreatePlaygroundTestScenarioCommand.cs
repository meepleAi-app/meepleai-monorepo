using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;

/// <summary>
/// Command to create a new playground test scenario.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record CreatePlaygroundTestScenarioCommand(
    string Name,
    string Description,
    ScenarioCategory Category,
    List<ScenarioMessage> Messages,
    Guid CreatedBy,
    string? ExpectedOutcome = null,
    Guid? AgentDefinitionId = null,
    List<string>? Tags = null
) : IRequest<PlaygroundTestScenarioDto>;
