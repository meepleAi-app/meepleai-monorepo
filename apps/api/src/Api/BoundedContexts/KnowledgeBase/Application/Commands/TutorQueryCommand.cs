using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// ISSUE-3499: Query Tutor agent with multi-turn dialogue support.
/// Routes to orchestration-service for LangGraph-based processing.
/// </summary>
internal record TutorQueryCommand(
    Guid GameId,
    Guid SessionId,
    string Query
) : IRequest<TutorQueryResponse>;

/// <summary>
/// Response from Tutor agent query.
/// </summary>
internal record TutorQueryResponse(
    string Response,
    string AgentType,
    double Confidence,
    List<string> Citations,
    double ExecutionTimeMs
);
