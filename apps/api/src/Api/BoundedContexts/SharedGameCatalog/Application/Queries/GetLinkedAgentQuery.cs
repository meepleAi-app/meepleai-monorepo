using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get the linked AI agent definition for a shared game.
/// Returns null if no agent is linked.
/// Issue #4924
/// </summary>
internal record GetLinkedAgentQuery(Guid SharedGameId) : IQuery<AgentDefinitionDto?>;
