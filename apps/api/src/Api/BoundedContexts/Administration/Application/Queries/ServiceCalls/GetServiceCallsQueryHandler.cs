using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal sealed class GetServiceCallsQueryHandler
    : IQueryHandler<GetServiceCallsQuery, GetServiceCallsResponse>
{
    private readonly IServiceCallLogRepository _repo;

    public GetServiceCallsQueryHandler(IServiceCallLogRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GetServiceCallsResponse> Handle(
        GetServiceCallsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Min(query.PageSize, 100);
        var page = Math.Max(query.Page, 1);

        var (items, totalCount) = await _repo.GetPagedAsync(
            serviceName: query.ServiceName,
            isSuccess: query.IsSuccess,
            correlationId: query.CorrelationId,
            from: query.From,
            toUtc: query.To,
            minLatencyMs: query.MinLatencyMs,
            page: page,
            pageSize: pageSize,
            ct: cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(e => new ServiceCallDto(
            Id: e.Id,
            ServiceName: e.ServiceName,
            HttpMethod: e.HttpMethod,
            RequestUrl: e.RequestUrl,
            StatusCode: e.StatusCode,
            LatencyMs: e.LatencyMs,
            IsSuccess: e.IsSuccess,
            ErrorMessage: e.ErrorMessage,
            CorrelationId: e.CorrelationId,
            TimestampUtc: e.TimestampUtc,
            RequestSummary: e.RequestSummary,
            ResponseSummary: e.ResponseSummary
        )).ToList();

        return new GetServiceCallsResponse(dtos, totalCount, page, pageSize);
    }
}
