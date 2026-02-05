using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to reject an agent typology (admin only).
/// Transitions status from Pending to Rejected.
/// Issue #3381: Typology Approval Workflow Endpoint.
/// </summary>
internal record RejectAgentTypologyCommand(
    Guid Id,
    Guid RejectedBy,
    string Reason
) : IRequest<AgentTypologyDto>;
