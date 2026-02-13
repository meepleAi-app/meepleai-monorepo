using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to retrieve PDF processing metrics for performance tracking.
/// Issue #4219: Duration metrics and ETA calculation.
/// </summary>
/// <param name="DocumentId">PDF document ID to retrieve metrics for</param>
internal record GetPdfMetricsQuery(Guid DocumentId) : IQuery<PdfMetricsDto>;
