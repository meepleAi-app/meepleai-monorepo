using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.PlaygroundTestScenario;

/// <summary>
/// Query to get all playground test scenarios with optional filters.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record GetPlaygroundTestScenariosQuery(
    ScenarioCategory? Category = null,
    Guid? AgentDefinitionId = null,
    bool ActiveOnly = true
) : IRequest<List<PlaygroundTestScenarioDto>>;
