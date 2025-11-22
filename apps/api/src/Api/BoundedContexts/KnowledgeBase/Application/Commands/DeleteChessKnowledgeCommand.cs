using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to delete all chess knowledge from the vector database.
/// Use with caution - this operation cannot be undone.
/// </summary>
public sealed record DeleteChessKnowledgeCommand : IRequest<bool>
{
    // No parameters needed - deletes all chess category data
}
