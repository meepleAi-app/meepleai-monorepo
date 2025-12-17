using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GenerateReportCommand
/// ISSUE-916: On-demand report generation
/// </summary>
internal sealed class GenerateReportCommandHandler : ICommandHandler<GenerateReportCommand, GenerateReportResult>
{
    private readonly IReportGeneratorService _reportGenerator;
    private readonly IReportExecutionRepository _executionRepository;
    private readonly ILogger<GenerateReportCommandHandler> _logger;

    public GenerateReportCommandHandler(
        IReportGeneratorService reportGenerator,
        IReportExecutionRepository executionRepository,
        ILogger<GenerateReportCommandHandler> logger)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerateReportResult> Handle(GenerateReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation(
            "Processing GenerateReportCommand: {Template} in {Format}",
            command.Template, command.Format);

        // Create execution record
        var execution = ReportExecution.Create(Guid.Empty); // On-demand, no report ID
        await _executionRepository.AddAsync(execution, cancellationToken).ConfigureAwait(false);

        try
        {
            // Generate report
            var reportData = await _reportGenerator.GenerateAsync(
                command.Template,
                command.Format,
                command.Parameters,
                cancellationToken).ConfigureAwait(false);

            // Update execution as completed
            // Note: In production, outputPath would save to storage (S3, Azure Blob, etc.)
            var completedExecution = execution.Complete(
                outputPath: $"temp://{reportData.FileName}",
                fileSizeBytes: reportData.FileSizeBytes);

            await _executionRepository.UpdateAsync(completedExecution, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Report generated successfully: ExecutionId={ExecutionId}, FileName={FileName}",
                completedExecution.Id, reportData.FileName);

            return new GenerateReportResult(
                ExecutionId: completedExecution.Id,
                FileName: reportData.FileName,
                Content: reportData.Content,
                FileSizeBytes: reportData.FileSizeBytes);
        }
        catch (Exception ex)
        {
            // S2139: Logging removed to avoid double logging (caller will log).
            // We still need to update the execution status in the DB.

            // Update execution as failed
            var failedExecution = execution.Fail(ex.Message);
            await _executionRepository.UpdateAsync(failedExecution, cancellationToken).ConfigureAwait(false);

            throw;
        }
    }
}

