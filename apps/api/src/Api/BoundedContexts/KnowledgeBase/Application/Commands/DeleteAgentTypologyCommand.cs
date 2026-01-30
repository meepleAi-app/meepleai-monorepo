using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to soft delete an agent typology (admin only).
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal record DeleteAgentTypologyCommand(
    Guid Id
) : IRequest;
