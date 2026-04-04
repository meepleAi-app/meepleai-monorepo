using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get model/provider/operation distribution breakdowns.
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal record GetMyAiUsageDistributionsQuery(Guid UserId, int Days = 30) : IQuery<AiUsageDistributionsDto>;
