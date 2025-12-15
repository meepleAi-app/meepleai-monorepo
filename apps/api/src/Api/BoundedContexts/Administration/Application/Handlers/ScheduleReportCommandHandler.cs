using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for ScheduleReportCommand
/// ISSUE-916: Report scheduling with Quartz.NET
/// </summary>
internal sealed class ScheduleReportCommandHandler : ICommandHandler<ScheduleReportCommand, Guid>
{
    private readonly IAdminReportRepository _repository;
    private readonly IReportSchedulerService _schedulerService;
    private readonly ILogger<ScheduleReportCommandHandler> _logger;

    public ScheduleReportCommandHandler(
        IAdminReportRepository repository,
        IReportSchedulerService schedulerService,
        ILogger<ScheduleReportCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(ScheduleReportCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation(
            "Scheduling {Template} report: {Name}",
            command.Template, command.Name);

        // ISSUE-916: Validate cron expression (SECURITY)
        if (!Quartz.CronExpression.IsValidExpression(command.ScheduleExpression))
        {
            throw new ArgumentException(
                $"Invalid cron expression: {command.ScheduleExpression}",
                nameof(command));
        }

        // Create domain entity (ISSUE-918: with email recipients)
        var report = AdminReport.Create(
            name: command.Name,
            description: command.Description,
            template: command.Template,
            format: command.Format,
            parameters: command.Parameters,
            scheduleExpression: command.ScheduleExpression,
            createdBy: command.CreatedBy,
            emailRecipients: command.EmailRecipients);

        // Save to database
        await _repository.AddAsync(report, ct).ConfigureAwait(false);

        // Schedule with Quartz.NET
        await _schedulerService.ScheduleReportAsync(report, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Report scheduled successfully: ReportId={ReportId}, Schedule={Schedule}",
            report.Id, report.ScheduleExpression);

        return report.Id;
    }
}
