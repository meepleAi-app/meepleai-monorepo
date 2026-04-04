using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal record GetServiceCallSummaryQuery(string? Period = "24h") : IQuery<IReadOnlyList<ServiceCallSummaryDto>>;
