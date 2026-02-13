using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF processing analytics.
/// Issue #3715.2: System-wide analytics with optional user filtering.
/// </summary>
/// <param name="TimeRangeDays">Number of days to analyze (7, 30, 90)</param>
/// <param name="UserId">Optional: Filter to specific user (null = all users)</param>
/// <param name="UserRole">User's role for filtering (admin sees all, user sees own)</param>
public record GetPdfAnalyticsQuery(
    int TimeRangeDays,
    Guid? UserId,
    string UserRole
) : IRequest<PdfAnalyticsDto>;
