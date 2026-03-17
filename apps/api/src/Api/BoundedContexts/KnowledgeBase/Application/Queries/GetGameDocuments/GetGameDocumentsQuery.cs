using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameDocuments;

/// <summary>
/// Query to retrieve KB documents linked to a specific game.
/// Returns documents ordered by CreatedAt descending.
/// </summary>
internal sealed record GetGameDocumentsQuery(
    Guid GameId,
    Guid UserId
) : IQuery<IReadOnlyList<GameDocumentDto>>;
