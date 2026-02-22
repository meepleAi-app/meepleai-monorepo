using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns a timeline of LLM requests grouped by source and time bucket.
/// Issue #5078: Admin usage page — request timeline chart.
/// </summary>
/// <param name="Period">One of: "24h", "7d", "30d".</param>
public sealed record GetUsageTimelineQuery(string Period = "24h") : IRequest<UsageTimelineDto>;
