using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3447 slice: returns the persisted hi_res image-table regions for a PDF (for the viewer overlay).
/// Per-user authz/copyright gating is deferred (slice spec S-4); the endpoint only requires a session.
/// </summary>
internal sealed record GetPdfImageRegionsQuery(Guid PdfId) : IQuery<IReadOnlyList<ImageRegionDto>>;

internal record ImageRegionDto(int Page, double X, double Y, double Width, double Height, string ElementType);
