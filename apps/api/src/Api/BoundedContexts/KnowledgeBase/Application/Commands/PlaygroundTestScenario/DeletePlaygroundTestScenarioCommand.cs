using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;

/// <summary>
/// Command to soft-delete a playground test scenario (deactivation).
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed record DeletePlaygroundTestScenarioCommand(Guid Id) : IRequest;
