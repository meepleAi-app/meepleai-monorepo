using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Handles <see cref="GetKbDocumentByIdQuery"/>.
///
/// Joins <c>PdfDocumentEntity</c> (primary) with <c>VectorDocumentEntity</c> (optional LEFT JOIN)
/// so that documents still being processed (no VectorDocument yet) are returned with
/// <c>TotalChunks = 0</c> and <c>IndexedAt = null</c> rather than a 404.
///
/// Admin-only fields (<c>ProcessingError</c>, <c>RetryCount</c>, <c>FailedAtState</c>) are
/// populated only when <see cref="GetKbDocumentByIdQuery.UserIsAdmin"/> is <c>true</c>.
/// </summary>
internal sealed class GetKbDocumentByIdHandler : IQueryHandler<GetKbDocumentByIdQuery, KbDocumentDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;

    public GetKbDocumentByIdHandler(MeepleAiDbContext dbContext, ILogger<GetKbDocumentByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbDocumentDto> Handle(GetKbDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug("Fetching KB document {DocId} (admin={IsAdmin})", query.DocumentId, query.UserIsAdmin);

        var data = await (
            from pdf in _dbContext.PdfDocuments.AsNoTracking()
            join vd in _dbContext.VectorDocuments.AsNoTracking()
                on pdf.Id equals vd.PdfDocumentId into vdj
            from vd in vdj.DefaultIfEmpty()
            where pdf.Id == query.DocumentId
            select new
            {
                pdf,
                TotalChunks = vd != null ? vd.ChunkCount : 0,
                IndexedAt = vd != null ? vd.IndexedAt : (DateTime?)null,
                GameId = vd != null ? vd.GameId : (Guid?)null
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            throw new NotFoundException($"KB document {query.DocumentId} not found");
        }

        // Issue #730 final review: enforce access control
        if (!data.pdf.IsPublic
            && data.pdf.UploadedByUserId != query.RequestingUserId
            && !query.UserIsAdmin)
        {
            throw new ForbiddenException($"Access denied to document {query.DocumentId}");
        }

        return new KbDocumentDto(
            Id: data.pdf.Id,
            Title: data.pdf.FileName,
            GameId: data.GameId,
            SharedGameId: data.pdf.SharedGameId,
            DocumentCategory: data.pdf.DocumentCategory,
            ProcessingState: data.pdf.ProcessingState.ToLowerInvariant(),
            TotalChunks: data.TotalChunks,
            PageCount: data.pdf.PageCount ?? 0,
            IndexedAt: data.IndexedAt,
            UploadedAt: data.pdf.UploadedAt,
            Language: data.pdf.Language,
            VersionLabel: data.pdf.VersionLabel,
            ProcessingError: query.UserIsAdmin ? data.pdf.ProcessingError : null,
            RetryCount: query.UserIsAdmin && data.pdf.RetryCount > 0 ? data.pdf.RetryCount : (int?)null,
            FailedAtState: query.UserIsAdmin ? data.pdf.FailedAtState : null
        );
    }
}
