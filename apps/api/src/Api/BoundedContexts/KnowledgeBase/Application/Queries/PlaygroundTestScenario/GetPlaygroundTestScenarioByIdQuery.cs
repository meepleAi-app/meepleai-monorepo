using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.PlaygroundTestScenario;

/// <summary>
/// Query to get a playground test scenario by ID.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record GetPlaygroundTestScenarioByIdQuery(Guid Id) : IRequest<PlaygroundTestScenarioDto?>;
