using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns aggregated LLM cost data broken down by model, source, and user tier.
/// Issue #5080: Admin usage page — cost breakdown panel.
/// </summary>
/// <param name="Period">One of: "1d", "7d", "30d".</param>
public sealed record GetUsageCostsQuery(string Period = "7d") : IRequest<UsageCostsDto>;
