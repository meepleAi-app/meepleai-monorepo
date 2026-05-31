using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to fetch a single AI request log with drill payload (#1728):
/// retrieved chunks + per-stage latency breakdown.
/// Returns null when the id does not exist.
/// </summary>
internal record GetAiQueryDrillQuery(Guid Id) : IQuery<AiQueryDrillResult?>;
