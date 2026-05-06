using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Query to retrieve full metadata for a single KB document by its PDF document ID.
/// Used by <c>GET /api/v1/kb-docs/{id}</c> (G4 goal).
/// Admin-only fields (processingError, retryCount, failedAtState) are gated via <see cref="UserIsAdmin"/>.
/// </summary>
internal sealed record GetKbDocumentByIdQuery(
    Guid DocumentId,
    bool UserIsAdmin
) : IQuery<KbDocumentDto>;
