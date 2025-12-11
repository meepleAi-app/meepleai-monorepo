using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain entity representing an admin report definition
/// ISSUE-916: Report generation and scheduling
/// </summary>
public sealed record AdminReport
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ReportTemplate Template { get; init; }
    public required ReportFormat Format { get; init; }
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }
    public required string? ScheduleExpression { get; init; } // Cron expression or null if on-demand
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? LastExecutedAt { get; init; }
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Creates a new report definition
    /// </summary>
    public static AdminReport Create(
        string name,
        string description,
        ReportTemplate template,
        ReportFormat format,
        IReadOnlyDictionary<string, object>? parameters,
        string? scheduleExpression,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy, nameof(createdBy));

        return new AdminReport
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Template = template,
            Format = format,
            Parameters = parameters ?? new Dictionary<string, object>(),
            ScheduleExpression = scheduleExpression,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = createdBy
        };
    }

    /// <summary>
    /// Updates the last execution timestamp
    /// </summary>
    public AdminReport WithLastExecutedAt(DateTime executedAt)
    {
        return this with { LastExecutedAt = executedAt };
    }

    /// <summary>
    /// Deactivates the report
    /// </summary>
    public AdminReport Deactivate()
    {
        return this with { IsActive = false };
    }

    /// <summary>
    /// Activates the report
    /// </summary>
    public AdminReport Activate()
    {
        return this with { IsActive = true };
    }
}
