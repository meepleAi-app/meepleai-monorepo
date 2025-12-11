using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for UpdateReportScheduleCommand
/// ISSUE-916: Schedule update/cancellation
/// </summary>
public sealed class UpdateReportScheduleCommandHandler : ICommandHandler<UpdateReportScheduleCommand, bool>
{
    private readonly IAdminReportRepository _repository;
    private readonly IReportSchedulerService _schedulerService;
    private readonly ILogger<UpdateReportScheduleCommandHandler> _logger;

    public UpdateReportScheduleCommandHandler(
        IAdminReportRepository repository,
        IReportSchedulerService schedulerService,
        ILogger<UpdateReportScheduleCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(UpdateReportScheduleCommand command, CancellationToken ct)
    {
        _logger.LogInformation(
            "Updating schedule for report {ReportId}",
            command.ReportId);

        // Get existing report
        var report = await _repository.GetByIdAsync(command.ReportId, ct).ConfigureAwait(false);
        if (report is null)
        {
            _logger.LogWarning("Report not found: {ReportId}", command.ReportId);
            return false;
        }

        // Unschedule old job
        await _schedulerService.UnscheduleReportAsync(report.Id, ct).ConfigureAwait(false);

        // Update domain entity
        var updatedReport = report with
        {
            ScheduleExpression = command.ScheduleExpression,
            IsActive = command.IsActive
        };

        await _repository.UpdateAsync(updatedReport, ct).ConfigureAwait(false);

        // Reschedule if active and has schedule
        if (command.IsActive && !string.IsNullOrWhiteSpace(command.ScheduleExpression))
        {
            await _schedulerService.ScheduleReportAsync(updatedReport, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Report rescheduled: ReportId={ReportId}, Schedule={Schedule}",
                updatedReport.Id, updatedReport.ScheduleExpression);
        }
        else
        {
            _logger.LogInformation(
                "Report schedule cancelled: ReportId={ReportId}",
                updatedReport.Id);
        }

        return true;
    }
}
