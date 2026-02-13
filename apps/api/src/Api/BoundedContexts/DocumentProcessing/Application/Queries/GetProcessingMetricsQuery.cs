using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to retrieve aggregated processing metrics for admin dashboard.
/// Issue #4212: Historical metrics endpoint.
/// </summary>
internal record GetProcessingMetricsQuery : IQuery<ProcessingMetricsDto>;
