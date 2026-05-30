using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbNavCounts;

/// <summary>
/// Returns counts for KbSubNav badges: active processing jobs + feedback last 7 days.
/// Issue #1655 (F3-FU-6).
/// </summary>
public sealed record GetKbNavCountsQuery() : IRequest<KbNavCountsDto>;
