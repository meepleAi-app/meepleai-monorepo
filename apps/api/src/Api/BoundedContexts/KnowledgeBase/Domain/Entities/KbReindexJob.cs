using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Aggregate root representing a KB reindex job for a game's PDF collection.
/// Issue #941 / ADR-057.
///
/// State machine transitions are enforced by the Mark* methods; invalid
/// transitions throw <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class KbReindexJob : AggregateRoot<Guid>
{
    private KbReindexJob() : base(Guid.Empty)
    {
        // EF Core
        Status = KbReindexJobStatus.Queued;
        CreatedAt = DateTime.UtcNow;
    }

    private KbReindexJob(Guid id, Guid gameId, Guid userId, int totalPdfs, DateTime createdAt)
        : base(id)
    {
        GameId = gameId;
        UserId = userId;
        TotalPdfs = totalPdfs;
        ProcessedPdfs = 0;
        Status = KbReindexJobStatus.Queued;
        CreatedAt = createdAt;
    }

    /// <summary>The game whose KB this job is reindexing.</summary>
    public Guid GameId { get; private set; }

    /// <summary>The user who triggered the reindex (for authorization on poll).</summary>
    public Guid UserId { get; private set; }

    /// <summary>Current status — one of <see cref="KbReindexJobStatus"/> constants.</summary>
    public string Status { get; private set; }

    /// <summary>Total number of PDFs the job will reindex (captured at enqueue time).</summary>
    public int TotalPdfs { get; private set; }

    /// <summary>Number of PDFs the background service has finished processing.</summary>
    public int ProcessedPdfs { get; private set; }

    /// <summary>When the job row was created (= enqueued).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>When the background service started processing the job.</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>When the job reached a terminal state (Completed or Failed).</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Reason captured when the job transitions to Failed (exception message).</summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Factory: create a fresh job in Queued state. Used by the POST handler.
    /// </summary>
    public static KbReindexJob Create(Guid gameId, Guid userId, int totalPdfs)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("GameId required", nameof(gameId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId required", nameof(userId));
        if (totalPdfs < 0) throw new ArgumentException("TotalPdfs must be non-negative", nameof(totalPdfs));
        return new KbReindexJob(Guid.NewGuid(), gameId, userId, totalPdfs, DateTime.UtcNow);
    }

    /// <summary>Transition Queued → Running. Called by the background service when it picks up the work.</summary>
    public void MarkRunning()
    {
        if (!string.Equals(Status, KbReindexJobStatus.Queued, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot transition {Status} → running");
        Status = KbReindexJobStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>Increments the processed-PDF counter (idempotency at the caller).</summary>
    public void IncrementProcessed()
    {
        if (!string.Equals(Status, KbReindexJobStatus.Running, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot increment processed in {Status}");
        if (ProcessedPdfs >= TotalPdfs)
            throw new InvalidOperationException("Processed PDF count cannot exceed total");
        ProcessedPdfs += 1;
    }

    /// <summary>Transition Running → Completed.</summary>
    public void MarkCompleted()
    {
        if (!string.Equals(Status, KbReindexJobStatus.Running, StringComparison.Ordinal)
            && !string.Equals(Status, KbReindexJobStatus.Queued, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot transition {Status} → completed");
        Status = KbReindexJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Transition Queued or Running → Failed.</summary>
    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("FailureReason required", nameof(reason));
        if (!string.Equals(Status, KbReindexJobStatus.Queued, StringComparison.Ordinal)
            && !string.Equals(Status, KbReindexJobStatus.Running, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot transition {Status} → failed");
        Status = KbReindexJobStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconstitute an aggregate from a persistence snapshot. Bypasses the
    /// factory's validation because the data has already passed validation
    /// at insert time. Used by the repository's MapToDomain.
    /// </summary>
    public static KbReindexJob Hydrate(
        Guid id,
        Guid gameId,
        Guid userId,
        string status,
        int totalPdfs,
        int processedPdfs,
        DateTime createdAt,
        DateTime? startedAt,
        DateTime? completedAt,
        string? failureReason)
    {
        var job = new KbReindexJob
        {
            GameId = gameId,
            UserId = userId,
            Status = status,
            TotalPdfs = totalPdfs,
            ProcessedPdfs = processedPdfs,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            FailureReason = failureReason
        };
        // Set Id through the protected setter exposed by Entity<TId>.
        job.GetType().BaseType!.BaseType!
            .GetProperty(nameof(Id))!
            .SetValue(job, id);
        return job;
    }
}
