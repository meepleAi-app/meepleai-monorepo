using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Query to retrieve a cursor-paginated list of chunk summaries for a single
/// KB document. Used by <c>GET /api/v1/kb-docs/{id}/chunks</c>.
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cursor</b>: opaque base64-encoded <c>(Position, Id)</c> tuple. <c>null</c>
/// for first page. Caller decodes via <see cref="KbChunksCursor.Decode"/> and
/// returns 400 Bad Request on <see cref="FormatException"/>.
/// </para>
///
/// <para>
/// <b>Limit</b>: clamped 1..100 by validator; FE default is 50 per spec.
/// </para>
///
/// <para>
/// Access denied (<c>ForbiddenException</c>) when the document is private,
/// not owned by <see cref="RequestingUserId"/>, and the requester is not admin.
/// </para>
/// </remarks>
internal sealed record GetKbChunksQuery(
    Guid DocumentId,
    Guid RequestingUserId,
    KbChunksCursor.CursorPayload? Cursor,
    int Limit,
    bool UserIsAdmin
) : IQuery<KbChunksListResponse>;
