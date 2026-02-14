using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;

/// <summary>
/// Command to update an existing playground test scenario.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record UpdatePlaygroundTestScenarioCommand(
    Guid Id,
    string Name,
    string Description,
    ScenarioCategory Category,
    List<ScenarioMessage> Messages,
    string? ExpectedOutcome = null,
    List<string>? Tags = null
) : IRequest;
