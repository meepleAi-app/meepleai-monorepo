using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ProcessingJob aggregate.
/// Maps between domain entities and EF persistence entities.
/// Issue #4731: Queue commands/queries.
/// </summary>
internal class ProcessingJobRepository : RepositoryBase, IProcessingJobRepository
{
    public ProcessingJobRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ProcessingJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ProcessingJobs
            .Include(j => j.Steps)
                .ThenInclude(s => s.LogEntries)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<ProcessingJob?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessingJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ProcessingJobs
            .Include(j => j.Steps)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(job);
        var entity = MapToPersistence(job);
        await DbContext.ProcessingJobs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ProcessingJob job, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(job);
        var entity = MapToPersistence(job);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<ProcessingJobEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);
        if (tracked != null)
            tracked.State = EntityState.Detached;

        // Detach tracked step entities
        foreach (var stepEntity in entity.Steps)
        {
            var trackedStep = DbContext.ChangeTracker.Entries<ProcessingStepEntity>()
                .FirstOrDefault(e => e.Entity.Id == stepEntity.Id);
            if (trackedStep != null)
                trackedStep.State = EntityState.Detached;
        }

        DbContext.ProcessingJobs.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ProcessingJob job, CancellationToken cancellationToken = default)
    {
        var entity = new ProcessingJobEntity { Id = job.Id };
        DbContext.ProcessingJobs.Attach(entity);
        DbContext.ProcessingJobs.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ProcessingJobs.AnyAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        var statusStr = status.ToString();
        return await DbContext.ProcessingJobs
            .CountAsync(j => j.Status == statusStr, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByPdfDocumentIdAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default)
    {
        return await DbContext.ProcessingJobs
            .AnyAsync(j => j.PdfDocumentId == pdfDocumentId
                && (j.Status == "Queued" || j.Status == "Processing"), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ProcessingJob> Items, int Total)> GetPaginatedAsync(
        JobStatus? statusFilter,
        string? searchText,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.ProcessingJobs
            .Include(j => j.Steps)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            var statusStr = statusFilter.Value.ToString();
            query = query.Where(j => j.Status == statusStr);
        }

        if (fromDate.HasValue)
            query = query.Where(j => j.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(j => j.CreatedAt <= toDate.Value);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToList(), total);
    }

    public async Task<ProcessingJob?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        // Priority DESC (higher = more important), then FIFO within same priority
        var entity = await DbContext.ProcessingJobs
            .Include(j => j.Steps)
                .ThenInclude(s => s.LogEntries)
            .Where(j => j.Status == nameof(JobStatus.Queued))
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<int> CountProcessingAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.ProcessingJobs
            .CountAsync(j => j.Status == nameof(JobStatus.Processing), cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessingJob MapToDomain(ProcessingJobEntity entity)
    {
        var steps = entity.Steps
            .OrderBy(s => s.StepName, StringComparer.Ordinal)
            .Select(MapStepToDomain)
            .ToList();

        return ProcessingJob.Reconstitute(
            id: entity.Id,
            pdfDocumentId: entity.PdfDocumentId,
            userId: entity.UserId,
            status: Enum.Parse<JobStatus>(entity.Status),
            priority: entity.Priority,
            currentStep: string.IsNullOrEmpty(entity.CurrentStep)
                ? null
                : Enum.Parse<ProcessingStepType>(entity.CurrentStep),
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            completedAt: entity.CompletedAt,
            errorMessage: entity.ErrorMessage,
            retryCount: entity.RetryCount,
            maxRetries: entity.MaxRetries,
            steps: steps);
    }

    private static ProcessingStep MapStepToDomain(ProcessingStepEntity entity)
    {
        var logEntries = entity.LogEntries
            .OrderBy(l => l.Timestamp)
            .Select(MapLogToDomain)
            .ToList();

        return ProcessingStep.Reconstitute(
            id: entity.Id,
            stepName: Enum.Parse<ProcessingStepType>(entity.StepName),
            status: Enum.Parse<StepStatus>(entity.Status),
            startedAt: entity.StartedAt,
            completedAt: entity.CompletedAt,
            duration: entity.DurationMs.HasValue ? TimeSpan.FromMilliseconds(entity.DurationMs.Value) : null,
            metadataJson: entity.MetadataJson,
            logEntries: logEntries);
    }

    private static StepLogEntry MapLogToDomain(StepLogEntryEntity entity)
    {
        return StepLogEntry.Reconstitute(
            id: entity.Id,
            level: Enum.Parse<StepLogLevel>(entity.Level),
            message: entity.Message,
            timestamp: entity.Timestamp);
    }

    private static ProcessingJobEntity MapToPersistence(ProcessingJob job)
    {
        var entity = new ProcessingJobEntity
        {
            Id = job.Id,
            PdfDocumentId = job.PdfDocumentId,
            UserId = job.UserId,
            Status = job.Status.ToString(),
            Priority = job.Priority,
            CurrentStep = job.CurrentStep?.ToString(),
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage,
            RetryCount = job.RetryCount,
            MaxRetries = job.MaxRetries,
            Steps = job.Steps.Select(MapStepToPersistence).ToList()
        };

        return entity;
    }

    private static ProcessingStepEntity MapStepToPersistence(ProcessingStep step)
    {
        return new ProcessingStepEntity
        {
            Id = step.Id,
            StepName = step.StepName.ToString(),
            Status = step.Status.ToString(),
            StartedAt = step.StartedAt,
            CompletedAt = step.CompletedAt,
            DurationMs = step.Duration?.TotalMilliseconds,
            MetadataJson = step.MetadataJson,
            LogEntries = step.LogEntries.Select(MapLogToPersistence).ToList()
        };
    }

    private static StepLogEntryEntity MapLogToPersistence(StepLogEntry log)
    {
        return new StepLogEntryEntity
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            Level = log.Level.ToString(),
            Message = log.Message
        };
    }
}
