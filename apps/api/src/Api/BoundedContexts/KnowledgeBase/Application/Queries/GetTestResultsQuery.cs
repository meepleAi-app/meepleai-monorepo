using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get test results for a typology.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal record GetTestResultsQuery(
    Guid? TypologyId = null,
    Guid? ExecutedBy = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? SavedOnly = null,
    int Skip = 0,
    int Take = 50
) : IRequest<AgentTestResultListDto>;
