using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain entity representing an admin report definition
/// ISSUE-916: Report generation and scheduling
/// </summary>
internal sealed record AdminReport
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
    public required IReadOnlyList<string> EmailRecipients { get; init; } // ISSUE-918: Email delivery integration

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
        string createdBy,
        IReadOnlyList<string>? emailRecipients = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy, nameof(createdBy));

        var validatedRecipients = ValidateEmailRecipients(emailRecipients);

        return new AdminReport
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Template = template,
            Format = format,
            Parameters = parameters ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ScheduleExpression = scheduleExpression,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastExecutedAt = null,
            CreatedBy = createdBy,
            EmailRecipients = validatedRecipients
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

    /// <summary>
    /// Updates email recipients
    /// ISSUE-918: Email delivery configuration
    /// </summary>
    public AdminReport WithEmailRecipients(IReadOnlyList<string> emailRecipients)
    {
        var validatedRecipients = ValidateEmailRecipients(emailRecipients);
        return this with { EmailRecipients = validatedRecipients };
    }

    /// <summary>
    /// Validates email recipients list
    /// ISSUE-918: Security - prevent injection and spam
    /// </summary>
    private static IReadOnlyList<string> ValidateEmailRecipients(IReadOnlyList<string>? recipients)
    {
        if (recipients is null || recipients.Count == 0)
        {
            return Array.Empty<string>();
        }

        const int MaxRecipients = 10;
        if (recipients.Count > MaxRecipients)
        {
            throw new ArgumentException($"Maximum {MaxRecipients} email recipients allowed", nameof(recipients));
        }

        var emailRegex = new System.Text.RegularExpressions.Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var validatedList = new List<string>();
        foreach (var email in recipients)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (!emailRegex.IsMatch(trimmedEmail))
            {
                throw new ArgumentException($"Invalid email address: {email}", nameof(recipients));
            }

            if (!validatedList.Contains(trimmedEmail, StringComparer.Ordinal))
            {
                validatedList.Add(trimmedEmail);
            }
        }

        return validatedList.AsReadOnly();
    }
}
