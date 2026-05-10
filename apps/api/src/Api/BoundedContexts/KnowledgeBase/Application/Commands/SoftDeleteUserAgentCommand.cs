using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to soft-delete a user-owned agent definition.
///
/// Soft-delete semantics:
/// - Sets IsDeleted=true + DeletedAt=now on the AgentDefinition aggregate
/// - Cascades CloseThread() on all active ChatThreads associated with the agent
/// - The agent becomes invisible to normal queries (global EF query filter)
/// - System-defined agents (IsSystemDefined=true) are rejected with SystemAgentProtectedException (403)
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// </summary>
internal sealed record SoftDeleteUserAgentCommand(
    Guid UserId,
    Guid AgentId
) : IRequest;
