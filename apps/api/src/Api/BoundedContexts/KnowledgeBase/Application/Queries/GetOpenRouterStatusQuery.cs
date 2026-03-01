using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query that returns a real-time snapshot of OpenRouter account status,
/// rate-limit utilization, and today's request count for the admin usage dashboard.
/// Issue #5077: Admin usage page — KPI cards data source.
/// </summary>
public sealed record GetOpenRouterStatusQuery : IRequest<OpenRouterStatusDto>;
