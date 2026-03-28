using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal record GetServiceCallsQuery(
    string? ServiceName, bool? IsSuccess, string? CorrelationId,
    DateTime? From, DateTime? To, long? MinLatencyMs,
    int Page = 1, int PageSize = 50
) : IQuery<GetServiceCallsResponse>;

public sealed record GetServiceCallsResponse(
    IReadOnlyList<ServiceCallDto> Items, int TotalCount, int Page, int PageSize);
