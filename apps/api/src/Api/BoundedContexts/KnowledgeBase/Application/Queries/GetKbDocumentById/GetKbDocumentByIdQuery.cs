using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Query to retrieve full metadata for a single KB document by its PDF document ID.
/// Used by <c>GET /api/v1/kb-docs/{id}</c> (G4 goal).
/// Admin-only fields (processingError, retryCount, failedAtState) are gated via <see cref="UserIsAdmin"/>.
/// Access is denied (ForbiddenException) when the document is not public, not owned by
/// <see cref="RequestingUserId"/>, and the requester is not an admin.
/// </summary>
internal sealed record GetKbDocumentByIdQuery(
    Guid DocumentId,
    Guid RequestingUserId,
    bool UserIsAdmin
) : IQuery<KbDocumentDto>;
