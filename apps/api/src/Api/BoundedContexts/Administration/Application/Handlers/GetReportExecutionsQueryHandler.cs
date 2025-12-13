using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetReportExecutionsQuery
/// ISSUE-916: Execution history retrieval
/// </summary>
public sealed class GetReportExecutionsQueryHandler : IQueryHandler<GetReportExecutionsQuery, IReadOnlyList<ReportExecutionDto>>
{
    private readonly IReportExecutionRepository _repository;
    private readonly ILogger<GetReportExecutionsQueryHandler> _logger;

    public GetReportExecutionsQueryHandler(
        IReportExecutionRepository repository,
        ILogger<GetReportExecutionsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ReportExecutionDto>> Handle(GetReportExecutionsQuery query, CancellationToken ct)
    {
        _logger.LogDebug("Getting report executions: ReportId={ReportId}, Limit={Limit}",
            query.ReportId, query.Limit);

        var executions = query.ReportId.HasValue
            ? await _repository.GetByReportIdAsync(query.ReportId.Value, query.Limit, ct).ConfigureAwait(false)
            : await _repository.GetRecentExecutionsAsync(query.Limit, ct).ConfigureAwait(false);

        var dtos = executions.Select(e => new ReportExecutionDto(
            Id: e.Id,
            ReportId: e.ReportId,
            StartedAt: e.StartedAt,
            CompletedAt: e.CompletedAt,
            Status: e.Status,
            ErrorMessage: e.ErrorMessage,
            OutputPath: e.OutputPath,
            FileSizeBytes: e.FileSizeBytes,
            Duration: e.Duration
        )).ToList();

        _logger.LogDebug("Found {Count} executions", dtos.Count);

        return dtos;
    }
}
