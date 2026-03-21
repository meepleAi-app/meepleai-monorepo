using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to publish an agent definition, making it visible to regular users.
/// </summary>
public sealed record PublishAgentDefinitionCommand(Guid Id) : IRequest;
