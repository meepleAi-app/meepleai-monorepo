using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to approve an agent typology (admin only).
/// Transitions status from Pending to Approved.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// Issue #3381: Added Notes parameter for approval workflow.
/// </summary>
internal record ApproveAgentTypologyCommand(
    Guid Id,
    Guid ApprovedBy,
    string? Notes = null
) : IRequest<AgentTypologyDto>;
