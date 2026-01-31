using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to update the selected documents for an agent's knowledge base.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal record UpdateAgentDocumentsCommand(
    Guid AgentId,
    IReadOnlyList<Guid> DocumentIds
) : IRequest<UpdateAgentDocumentsResult>;

/// <summary>
/// Result of updating agent documents.
/// </summary>
internal record UpdateAgentDocumentsResult(
    bool Success,
    string Message,
    Guid AgentId,
    int DocumentCount,
    string? ErrorCode = null
);
