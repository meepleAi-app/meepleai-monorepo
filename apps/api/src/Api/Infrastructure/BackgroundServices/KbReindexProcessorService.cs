using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Channels;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that consumes <see cref="KbReindexChannel"/> and runs the
/// per-PDF reindex work asynchronously. Pattern mirrors
/// <see cref="GameSuggestionProcessorService"/>.
///
/// Issue #941 / ADR-057.
/// </summary>
internal sealed class KbReindexProcessorService : BackgroundService
{
    private readonly KbReindexChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KbReindexProcessorService> _logger;

    public KbReindexProcessorService(
        KbReindexChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<KbReindexProcessorService> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KbReindexProcessorService started");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
#pragma warning disable CA1031 // BACKGROUND SERVICE: generic catch keeps the loop alive on per-job failures.
            try
            {
                await ProcessJobAsync(request, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "KbReindex job {JobId} for game {GameId} failed at the worker boundary",
                    request.JobId, request.GameId);
                await TryMarkJobFailedAsync(request.JobId, ex.Message, stoppingToken).ConfigureAwait(false);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("KbReindexProcessorService stopped");
    }

    private async Task ProcessJobAsync(KbReindexJobRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IKbReindexJobRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Load the job + its PDF list. The job row already exists (created by the
        // POST handler under the same UoW that wrote the channel event).
        var job = await jobRepository.GetByIdAsync(request.JobId, ct).ConfigureAwait(false);
        if (job is null)
        {
            _logger.LogWarning(
                "KbReindex job {JobId} not found in DB — channel/DB divergence; skipping",
                request.JobId);
            return;
        }

        if (job.TotalPdfs == 0)
        {
            // Edge case: enqueued for a game with no indexable PDFs. Mark done immediately.
            job.MarkCompleted();
            await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("KbReindex job {JobId} completed (0 PDFs)", request.JobId);
            return;
        }

        job.MarkRunning();
        await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        var pdfIds = await db.PdfDocuments
            .AsNoTracking()
            .Where(p =>
                (p.SharedGameId == request.GameId || p.PrivateGameId == request.GameId)
                && p.ExtractedText != null)
            .Select(p => p.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "KbReindex job {JobId}: re-indexing {Count} PDFs for game {GameId}",
            request.JobId, pdfIds.Count, request.GameId);

        foreach (var pdfId in pdfIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await mediator
                    .Send(new IndexPdfCommand(pdfId.ToString()), ct)
                    .ConfigureAwait(false);

                job.IncrementProcessed();
                await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "KbReindex job {JobId}: PDF {PdfId} failed",
                    request.JobId, pdfId);
                // Mark the whole job failed on first PDF error. Per-PDF partial-failure
                // breakdown is deferred per the matured #941 spec.
                job.MarkFailed($"PDF {pdfId} failed: {ex.Message}");
                await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                return;
            }
#pragma warning restore CA1031
        }

        job.MarkCompleted();
        await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation(
            "KbReindex job {JobId} completed ({Count} PDFs)",
            request.JobId, pdfIds.Count);
    }

    private async Task TryMarkJobFailedAsync(Guid jobId, string reason, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IKbReindexJobRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var job = await jobRepository.GetByIdAsync(jobId, ct).ConfigureAwait(false);
            if (job is null) return;
            if (job.Status is "completed" or "failed") return;
            job.MarkFailed(reason);
            await jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark KbReindex job {JobId} as failed", jobId);
        }
#pragma warning restore CA1031
    }
}
