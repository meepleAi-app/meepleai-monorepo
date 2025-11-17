using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve low-quality AI responses for admin review.
/// AI-11: Quality tracking endpoints
/// </summary>
public sealed record GetLowQualityResponsesQuery(
    int Limit = 100,
    int Offset = 0,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<LowQualityResponsesResult>;
