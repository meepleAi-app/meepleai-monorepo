using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Saved agent cost estimation scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
public sealed class CostScenario : AggregateRoot<Guid>
{
    /// <summary>User-defined name for this scenario</summary>
    public string Name { get; private set; }

    /// <summary>RAG strategy name (Fast, Balanced, Precise, etc.)</summary>
    public string Strategy { get; private set; }

    /// <summary>LLM model identifier</summary>
    public string ModelId { get; private set; }

    /// <summary>Estimated messages per day</summary>
    public int MessagesPerDay { get; private set; }

    /// <summary>Number of active users</summary>
    public int ActiveUsers { get; private set; }

    /// <summary>Average tokens per request (input + output)</summary>
    public int AvgTokensPerRequest { get; private set; }

    /// <summary>Calculated cost per single request (USD)</summary>
    public decimal CostPerRequest { get; private set; }

    /// <summary>Calculated daily projection (USD)</summary>
    public decimal DailyProjection { get; private set; }

    /// <summary>Calculated monthly projection (USD)</summary>
    public decimal MonthlyProjection { get; private set; }

    /// <summary>JSON array of warning messages</summary>
    public string? Warnings { get; private set; }

    /// <summary>User who created this scenario</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>When this scenario was created</summary>
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value
    private CostScenario() : base() { }
#pragma warning restore CS8618

    private CostScenario(
        Guid id,
        string name,
        string strategy,
        string modelId,
        int messagesPerDay,
        int activeUsers,
        int avgTokensPerRequest,
        decimal costPerRequest,
        decimal dailyProjection,
        decimal monthlyProjection,
        string? warnings,
        Guid createdByUserId) : base(id)
    {
        Name = name;
        Strategy = strategy;
        ModelId = modelId;
        MessagesPerDay = messagesPerDay;
        ActiveUsers = activeUsers;
        AvgTokensPerRequest = avgTokensPerRequest;
        CostPerRequest = costPerRequest;
        DailyProjection = dailyProjection;
        MonthlyProjection = monthlyProjection;
        Warnings = warnings;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new saved cost scenario.
    /// </summary>
    public static CostScenario Create(
        string name,
        string strategy,
        string modelId,
        int messagesPerDay,
        int activeUsers,
        int avgTokensPerRequest,
        decimal costPerRequest,
        decimal dailyProjection,
        decimal monthlyProjection,
        string? warnings,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(strategy))
            throw new ArgumentException("Strategy is required", nameof(strategy));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("ModelId is required", nameof(modelId));

        if (messagesPerDay < 0)
            throw new ArgumentException("MessagesPerDay cannot be negative", nameof(messagesPerDay));

        if (activeUsers < 0)
            throw new ArgumentException("ActiveUsers cannot be negative", nameof(activeUsers));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty", nameof(createdByUserId));

        return new CostScenario(
            Guid.NewGuid(),
            name.Trim(),
            strategy,
            modelId,
            messagesPerDay,
            activeUsers,
            avgTokensPerRequest,
            costPerRequest,
            dailyProjection,
            monthlyProjection,
            warnings,
            createdByUserId);
    }
}
