using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Query to retrieve per-model performance metrics aggregated from agent test results.
/// </summary>
internal record GetModelPerformanceQuery : IQuery<ModelPerformanceDto>;
