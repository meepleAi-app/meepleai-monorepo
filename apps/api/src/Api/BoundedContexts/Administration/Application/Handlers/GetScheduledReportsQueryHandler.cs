using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetScheduledReportsQuery
/// ISSUE-916: Report listing
/// </summary>
internal sealed class GetScheduledReportsQueryHandler : IQueryHandler<GetScheduledReportsQuery, IReadOnlyList<ScheduledReportDto>>
{
    private readonly IAdminReportRepository _repository;
    private readonly ILogger<GetScheduledReportsQueryHandler> _logger;

    public GetScheduledReportsQueryHandler(
        IAdminReportRepository repository,
        ILogger<GetScheduledReportsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ScheduledReportDto>> Handle(GetScheduledReportsQuery query, CancellationToken ct)
    {
        _logger.LogDebug("Getting all scheduled reports");

        var reports = await _repository.GetAllAsync(ct).ConfigureAwait(false);

        var dtos = reports.Select(r => new ScheduledReportDto(
            Id: r.Id,
            Name: r.Name,
            Description: r.Description,
            Template: r.Template,
            Format: r.Format,
            ScheduleExpression: r.ScheduleExpression,
            IsActive: r.IsActive,
            CreatedAt: r.CreatedAt,
            LastExecutedAt: r.LastExecutedAt,
            CreatedBy: r.CreatedBy
        )).ToList();

        _logger.LogDebug("Found {Count} scheduled reports", dtos.Count);

        return dtos;
    }
}
