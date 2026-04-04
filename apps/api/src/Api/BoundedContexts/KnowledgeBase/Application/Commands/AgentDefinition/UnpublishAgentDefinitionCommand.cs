using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;

/// <summary>
/// Command to unpublish an agent definition, returning it to Draft status.
/// </summary>
public sealed record UnpublishAgentDefinitionCommand(Guid Id) : IRequest;
