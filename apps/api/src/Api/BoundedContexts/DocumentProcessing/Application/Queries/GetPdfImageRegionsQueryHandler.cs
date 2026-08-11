using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal class GetPdfImageRegionsQueryHandler
    : IQueryHandler<GetPdfImageRegionsQuery, IReadOnlyList<ImageRegionDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;

    public GetPdfImageRegionsQueryHandler(MeepleAiDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<IReadOnlyList<ImageRegionDto>> Handle(
        GetPdfImageRegionsQuery query, CancellationToken cancellationToken)
    {
        // Owner-or-shared-game scoping (mirror GetPdfTextQueryHandler #3222): shared-game PDFs are
        // public (citation viewer); private PDFs require ownership; admins bypass. A missing or
        // unauthorized PDF returns an empty list — indistinguishable from a region-less PDF, so it
        // leaks neither existence nor another user's table/figure layout.
        var pdf = await _dbContext.PdfDocuments
            .Where(p => p.Id == query.PdfId)
            .AsNoTracking()
            .Select(p => new { p.SharedGameId, p.UploadedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            return Array.Empty<ImageRegionDto>();
        }

        var isSharedGamePdf = pdf.SharedGameId.HasValue;
        var isOwner = pdf.UploadedByUserId == query.UserId;
        if (!query.IsAdmin && !isSharedGamePdf && !isOwner)
        {
            return Array.Empty<ImageRegionDto>();
        }

        // #3435 §5quinquies: region boxes are Full-tier-only, matching the grounded-citation gate
        // (GroundedAnswerService emits CitationRegion only for CopyrightTier.Full). A Protected PDF's
        // region layout must not leak through this viewer-overlay path. Copyright-tier resolution is
        // owned by KnowledgeBase and consumed via IMediator (ADR-090); the tier is NOT admin-bypassed
        // (copyright, not access-control) — consistent with the grounded path.
        var tier = await _mediator
            .Send(new ResolvePdfCopyrightTierQuery(query.PdfId.ToString(), query.UserId), cancellationToken)
            .ConfigureAwait(false);
        if (tier != CopyrightTier.Full)
        {
            return Array.Empty<ImageRegionDto>();
        }

        var regions = await _dbContext.PdfImageRegions
            .Where(r => r.PdfDocumentId == query.PdfId)
            .OrderBy(r => r.PageNumber)
            .AsNoTracking()
            .Select(r => new ImageRegionDto(r.PageNumber, r.X, r.Y, r.Width, r.Height, r.ElementType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return regions;
    }
}
