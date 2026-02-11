using System.Globalization;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.BusinessSimulations.Application.Jobs;

/// <summary>
/// Monthly background job that generates a PDF ledger report for the previous month.
/// Runs on the 1st of each month at 06:00 UTC.
/// Issue #3724: Export Ledger - Scheduled Reports (Epic #3688)
/// </summary>
[DisallowConcurrentExecution]
internal sealed class MonthlyLedgerReportJob : IJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<MonthlyLedgerReportJob> _logger;

    public MonthlyLedgerReportJob(
        IMediator mediator,
        ILogger<MonthlyLedgerReportJob> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation("Starting monthly ledger report generation");

        try
        {
            // Calculate previous month date range
            var now = DateTime.UtcNow;
            var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstDayOfPreviousMonth = firstDayOfCurrentMonth.AddMonths(-1);
            var lastDayOfPreviousMonth = firstDayOfCurrentMonth.AddDays(-1);

            _logger.LogInformation(
                "Generating PDF report for period: {From} to {To}",
                firstDayOfPreviousMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                lastDayOfPreviousMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            var query = new ExportLedgerQuery(
                LedgerExportFormat.Pdf,
                firstDayOfPreviousMonth,
                lastDayOfPreviousMonth,
                Type: null,
                Category: null);

            var result = await _mediator.Send(query, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Monthly ledger report generated successfully: FileName={FileName}, Size={Size} bytes",
                result.FileName, result.Content.Length);

            context.Result = new
            {
                Success = true,
                Period = $"{firstDayOfPreviousMonth:yyyy-MM}",
                FileName = result.FileName,
                SizeBytes = result.Content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly ledger report");
            context.Result = new { Success = false, Error = ex.Message };
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
