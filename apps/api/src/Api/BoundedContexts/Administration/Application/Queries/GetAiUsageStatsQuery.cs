using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get AI usage statistics by model for charts.
/// Issue #2790: Admin Dashboard Charts - AI Usage DonutChart
/// </summary>
internal record GetAiUsageStatsQuery() : IQuery<AiUsageStatsDto[]>;
