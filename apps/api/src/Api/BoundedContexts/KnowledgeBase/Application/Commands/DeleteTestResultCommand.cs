using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete an agent test result.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal record DeleteTestResultCommand(
    Guid Id,
    Guid RequestedBy
) : IRequest<bool>;
