using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns today's usage for each free OpenRouter model with daily quota information.
/// Issue #5082: Admin usage page — free tier quota indicator.
/// </summary>
public sealed record GetUsageFreeQuotaQuery : IRequest<FreeQuotaDto>;
