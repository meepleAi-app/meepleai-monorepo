using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Read-only projection of user preference data from the users table.
/// Used by SystemConfiguration BC queries to read user settings
/// without loading the full User aggregate.
/// Maps to the same "users" table via ToView() — EF treats it as read-only (no migrations).
/// </summary>
[SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Private setters are required by EF Core for entity materialization")]
public class UserPreferences
{
    public Guid Id { get; private set; }
    public string Language { get; private set; } = "en";
    public bool EmailNotifications { get; private set; } = true;
    public string Theme { get; private set; } = "system";
    public int DataRetentionDays { get; private set; } = 90;

    // EF Core requires a parameterless constructor for entity materialization
    internal UserPreferences() { }
}
