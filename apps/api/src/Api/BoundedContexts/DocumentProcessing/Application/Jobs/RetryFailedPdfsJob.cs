using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Background job for automatic retry of failed PDF processing with exponential backoff.
/// Retries transient errors (Network, Service, Unknown) while respecting MaxRetries limit.
/// Runs every 5 minutes to check for retryable failed PDFs.
/// Issue #4208: PDF State Machine and Error Handling
/// </summary>
[DisallowConcurrentExecution]
internal sealed class RetryFailedPdfsJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<RetryFailedPdfsJob> _logger;

    // Transient error categories that should be retried automatically (as strings for EF query)
    private static readonly List<string> RetriableCategories = new()
    {
        ErrorCategory.Network.ToString(),
        ErrorCategory.Service.ToString(),
        ErrorCategory.Unknown.ToString()
    };

    public RetryFailedPdfsJob(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<RetryFailedPdfsJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "RetryFailedPdfsJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        var cancellationToken = context.CancellationToken;
        var retryCount = 0;
        var skipCount = 0;

        try
        {
            // Find failed PDFs with retriable errors that haven't exceeded max retries
            // Note: Using == instead of string.Equals in LINQ to Entities (EF Core limitation)
            var failedPdfs = await _dbContext.PdfDocuments
                .Where(p => p.ProcessingState == PdfProcessingState.Failed.ToString()
                         && p.RetryCount < 3
                         && p.ErrorCategory != null
                         && RetriableCategories.Contains(p.ErrorCategory))
                .Select(p => new
                {
                    p.Id,
                    p.UploadedByUserId,
                    p.RetryCount,
                    p.ErrorCategory,
                    p.ProcessingError
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Found {Count} failed PDFs eligible for automatic retry",
                failedPdfs.Count);

            foreach (var pdf in failedPdfs)
            {
                // Calculate exponential backoff delay based on retry count
                var backoffSeconds = CalculateBackoffDelay(pdf.RetryCount);

                _logger.LogDebug(
                    "Retrying PDF {PdfId} (attempt {Attempt}/3, backoff: {Delay}s, category: {Category})",
                    pdf.Id,
                    pdf.RetryCount + 1,
                    backoffSeconds,
                    pdf.ErrorCategory);

                // Apply backoff delay
                if (backoffSeconds > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(backoffSeconds),
                        cancellationToken).ConfigureAwait(false);
                }

                // Trigger retry via command (respects domain rules)
                try
                {
                    var command = new RetryPdfProcessingCommand(
                        pdf.Id,
                        pdf.UploadedByUserId // Job runs with uploader context
                    );

                    var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);

                    if (result.Success)
                    {
                        retryCount++;
                        _logger.LogInformation(
                            "Successfully retried PDF {PdfId}, new state: {State}",
                            pdf.Id,
                            result.CurrentState);
                    }
                    else
                    {
                        skipCount++;
                        _logger.LogWarning(
                            "Failed to retry PDF {PdfId}: {Message}",
                            pdf.Id,
                            result.Message);
                    }
                }
                catch (Exception ex)
                {
                    skipCount++;
                    _logger.LogError(
                        ex,
                        "Exception during retry of PDF {PdfId}",
                        pdf.Id);
                }
            }

            _logger.LogInformation(
                "RetryFailedPdfsJob completed: {Retried} retried, {Skipped} skipped",
                retryCount,
                skipCount);

            context.Result = new
            {
                Success = true,
                PdfsRetried = retryCount,
                PdfsSkipped = skipCount,
                TotalEligible = failedPdfs.Count
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryFailedPdfsJob failed");

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Calculate exponential backoff delay based on retry attempt number.
    /// Formula: delay = 2^retryCount seconds (1s, 2s, 4s for attempts 0, 1, 2)
    /// </summary>
    private static int CalculateBackoffDelay(int retryCount)
    {
        return retryCount switch
        {
            0 => 1,  // First retry: 1 second
            1 => 2,  // Second retry: 2 seconds
            2 => 4,  // Third retry: 4 seconds
            _ => 0   // No delay for out-of-bounds (shouldn't happen)
        };
    }
}
