using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to restore a previously soft-deleted user-owned agent definition.
///
/// Restore semantics:
/// - Sets IsDeleted=false + clears DeletedAt on the AgentDefinition aggregate
/// - The agent becomes visible again in normal queries
/// - ChatThreads that were closed during the delete remain closed (by design — per spec)
///   Rationale: open threads could contain stale context from before the delete;
///   users must explicitly reopen threads if they want to continue them.
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// </summary>
internal sealed record RestoreUserAgentCommand(
    Guid UserId,
    Guid AgentId
) : IRequest<AgentDto>;
