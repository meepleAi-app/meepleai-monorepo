using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3447 slice: returns the persisted hi_res image-table regions for a PDF (for the viewer overlay).
/// Owner-or-shared-game scoped (mirror <c>GetPdfTextQuery</c> #3222): shared-game PDFs are public,
/// private PDFs require ownership, admins bypass. Additionally copyright-tier gated to <c>Full</c>
/// (#3435 §5quinquies): the handler returns an empty overlay for non-Full tiers so a Protected PDF's
/// region layout does not leak, matching the grounded-citation region gate.
/// </summary>
internal sealed record GetPdfImageRegionsQuery(Guid PdfId, Guid UserId, bool IsAdmin)
    : IQuery<IReadOnlyList<ImageRegionDto>>;

internal record ImageRegionDto(int Page, double X, double Y, double Width, double Height, string ElementType);
