using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Singleton configuration entity for the PDF processing queue.
/// Stored in PostgreSQL for persistence across restarts.
/// Issue #5455: Queue configuration management.
/// </summary>
public sealed class ProcessingQueueConfig : AggregateRoot<Guid>
{
    public const int MinConcurrentWorkers = 1;
    public const int MaxConcurrentWorkersLimit = 10;
    public const int DefaultMaxConcurrentWorkers = 3;

    /// <summary>
    /// Well-known singleton ID for the queue configuration.
    /// </summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public bool IsPaused { get; private set; }
    public int MaxConcurrentWorkers { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ProcessingQueueConfig() : base() { }

    private ProcessingQueueConfig(DateTimeOffset now) : base(SingletonId)
    {
        IsPaused = false;
        MaxConcurrentWorkers = DefaultMaxConcurrentWorkers;
        UpdatedAt = now;
    }

    /// <summary>
    /// Create the default singleton configuration.
    /// </summary>
    public static ProcessingQueueConfig CreateDefault(TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new ProcessingQueueConfig(now);
    }

    /// <summary>
    /// Update queue configuration.
    /// </summary>
    public void Update(bool? isPaused, int? maxConcurrentWorkers, Guid updatedBy, TimeProvider? timeProvider = null)
    {
        if (maxConcurrentWorkers.HasValue)
        {
            if (maxConcurrentWorkers.Value < MinConcurrentWorkers || maxConcurrentWorkers.Value > MaxConcurrentWorkersLimit)
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentWorkers),
                    $"MaxConcurrentWorkers must be between {MinConcurrentWorkers} and {MaxConcurrentWorkersLimit}.");

            MaxConcurrentWorkers = maxConcurrentWorkers.Value;
        }

        if (isPaused.HasValue)
            IsPaused = isPaused.Value;

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Reconstitute from persistence.
    /// </summary>
    internal static ProcessingQueueConfig Reconstitute(
        Guid id,
        bool isPaused,
        int maxConcurrentWorkers,
        DateTimeOffset updatedAt,
        Guid? updatedBy)
    {
        var config = new ProcessingQueueConfig();
        typeof(ProcessingQueueConfig).BaseType!.BaseType!.GetProperty("Id")!.SetValue(config, id);
        config.IsPaused = isPaused;
        config.MaxConcurrentWorkers = maxConcurrentWorkers;
        config.UpdatedAt = updatedAt;
        config.UpdatedBy = updatedBy;
        return config;
    }
}
