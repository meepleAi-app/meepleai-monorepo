using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3447 slice: returns the persisted hi_res image-table regions for a PDF (for the viewer overlay).
/// Owner-or-shared-game scoped (mirror <c>GetPdfTextQuery</c> #3222): shared-game PDFs are public,
/// private PDFs require ownership, admins bypass. Copyright-tier gating remains deferred (slice spec S-4).
/// </summary>
internal sealed record GetPdfImageRegionsQuery(Guid PdfId, Guid UserId, bool IsAdmin)
    : IQuery<IReadOnlyList<ImageRegionDto>>;

internal record ImageRegionDto(int Page, double X, double Y, double Width, double Height, string ElementType);
