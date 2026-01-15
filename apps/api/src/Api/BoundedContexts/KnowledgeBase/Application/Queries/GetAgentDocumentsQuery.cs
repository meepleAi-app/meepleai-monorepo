using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get the selected documents for an agent's knowledge base.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal record GetAgentDocumentsQuery(Guid AgentId) : IRequest<AgentDocumentsDto?>;
