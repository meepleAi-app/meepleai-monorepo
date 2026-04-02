using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

/// <summary>
/// Command to remove a vector document from the KB of a specific game.
/// KB-02: Admin per-game KB backend.
/// </summary>
internal sealed record RemoveDocumentFromKbCommand(Guid VectorDocumentId, Guid GameId) : IRequest;
