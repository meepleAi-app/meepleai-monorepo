using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete a user-owned agent.
/// Owner or admin only.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
internal record DeleteUserAgentCommand(
    Guid AgentId,
    Guid UserId,
    string UserRole
) : IRequest<bool>;
