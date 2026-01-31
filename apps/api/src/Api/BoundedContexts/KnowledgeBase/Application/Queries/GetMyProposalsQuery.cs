using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get Editor's own typology proposals (Editor only).
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
/// <param name="EditorId">Editor user ID</param>
internal record GetMyProposalsQuery(Guid EditorId) : IRequest<List<AgentTypologyDto>>;
