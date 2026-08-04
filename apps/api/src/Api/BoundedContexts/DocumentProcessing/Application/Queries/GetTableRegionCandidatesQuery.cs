using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3435 SP2 (router DC-B): lists the PDFs that are candidates for the Metà-C VLM enrichment pass —
/// Ready, in the current KB corpus (IndexerVersion set), non-demo, and carrying at least
/// <paramref name="MinImageRegions"/> persisted image-table regions (see
/// <see cref="Domain.Services.TableRegionCandidateDecider"/>). This is the read side of the router
/// that gates the (expensive) VLM so it never runs on text-only PDFs (NFR1). Consumed today by the
/// admin verification endpoint and, later, by the SP4 VLM batch job.
/// </summary>
/// <param name="MinImageRegions">
/// Override for the candidate threshold. Null → config <c>PdfProcessing:TableRegionRouter:MinImageRegions</c>
/// → <see cref="Domain.Services.TableRegionCandidateDecider.DefaultMinImageRegions"/>.
/// </param>
/// <param name="Limit">Max candidates to return (null → a defensive default cap). Most-dense first.</param>
internal sealed record GetTableRegionCandidatesQuery(int? MinImageRegions = null, int? Limit = null)
    : IQuery<IReadOnlyList<TableRegionCandidateDto>>;

/// <summary>
/// A table-heavy PDF candidate. <paramref name="DistinctPageCount"/> is how many distinct pages carry
/// regions — a hint for the future VLM job of how many page crops it would render.
/// </summary>
internal sealed record TableRegionCandidateDto(Guid PdfDocumentId, int ImageRegionCount, int DistinctPageCount);
