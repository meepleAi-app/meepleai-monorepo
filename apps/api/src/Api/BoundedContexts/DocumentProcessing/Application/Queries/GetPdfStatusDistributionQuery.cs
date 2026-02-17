using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF status distribution for analytics.
/// PDF Storage Management Hub: Phase 6 analytics.
/// </summary>
internal record GetPdfStatusDistributionQuery() : IQuery<PdfStatusDistributionDto>;

internal record PdfStatusDistributionDto(
    Dictionary<string, int> CountByState,
    int TotalDocuments,
    List<PdfSizeRankItem> TopBySize
);

internal record PdfSizeRankItem(
    Guid Id,
    string FileName,
    long FileSizeBytes
);
