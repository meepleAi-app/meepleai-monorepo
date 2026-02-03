using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get a single test result by ID.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal record GetTestResultByIdQuery(
    Guid Id
) : IRequest<AgentTestResultDto?>;
