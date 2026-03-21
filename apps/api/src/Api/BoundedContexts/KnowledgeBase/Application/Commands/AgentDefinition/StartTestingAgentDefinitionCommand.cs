using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to transition an agent definition to Testing status.
/// </summary>
public sealed record StartTestingAgentDefinitionCommand(Guid Id) : IRequest;
