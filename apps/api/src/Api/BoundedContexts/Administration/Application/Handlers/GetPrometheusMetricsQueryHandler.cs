using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for Prometheus metrics queries.
/// Issue #893: Executes PromQL range queries via Prometheus HTTP API.
/// </summary>
public class GetPrometheusMetricsQueryHandler : IRequestHandler<GetPrometheusMetricsQuery, PrometheusMetricsResponse>
{
    private readonly IPrometheusQueryService _prometheusService;
    private readonly ILogger<GetPrometheusMetricsQueryHandler> _logger;

    public GetPrometheusMetricsQueryHandler(
        IPrometheusQueryService prometheusService,
        ILogger<GetPrometheusMetricsQueryHandler> logger)
    {
        _prometheusService = prometheusService;
        _logger = logger;
    }

    public async Task<PrometheusMetricsResponse> Handle(
        GetPrometheusMetricsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing Prometheus range query. Query: {Query}, Start: {Start}, End: {End}, Step: {Step}",
            request.Query, request.Start, request.End, request.Step);

        try
        {
            var result = await _prometheusService.QueryRangeAsync(
                request.Query,
                request.Start,
                request.End,
                request.Step,
                cancellationToken);

            var timeSeriesDtos = result.TimeSeries.Select(ts => new PrometheusTimeSeriesDto(
                ts.Metric,
                ts.Values.Select(v => new PrometheusDataPointDto(v.Timestamp, v.Value)).ToList()
            )).ToList();

            return new PrometheusMetricsResponse(result.ResultType, timeSeriesDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Prometheus query: {Query}", request.Query);
            throw;
        }
    }
}
