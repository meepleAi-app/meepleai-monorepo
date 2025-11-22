using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to index all chess knowledge into the vector database.
/// Loads chess knowledge from Data/ChessKnowledge.json and creates embeddings.
/// </summary>
public sealed record IndexChessKnowledgeCommand : IRequest<Api.Services.ChessIndexResult>
{
    // No parameters needed - loads from predefined data file
}
