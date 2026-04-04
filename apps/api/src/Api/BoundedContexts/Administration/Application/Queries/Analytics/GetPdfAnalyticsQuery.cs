using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Query to retrieve PDF processing analytics: totals, success rate,
/// and average processing time.
/// </summary>
internal record GetPdfAnalyticsQuery : IQuery<PdfAnalyticsDto>;
