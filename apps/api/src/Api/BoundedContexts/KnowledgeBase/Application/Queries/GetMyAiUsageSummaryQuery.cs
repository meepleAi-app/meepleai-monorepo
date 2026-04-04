using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get multi-period AI usage summary (today/7d/30d).
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal record GetMyAiUsageSummaryQuery(Guid UserId) : IQuery<AiUsageSummaryDto>;
