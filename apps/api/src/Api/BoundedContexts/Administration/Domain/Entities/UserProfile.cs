using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Read-only projection of user profile data from the users table.
/// Used by Administration BC queries to avoid loading the full User aggregate.
/// Maps to the same "users" table via ToView() — EF treats it as read-only (no migrations).
/// </summary>
[SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Private setters are required by EF Core for entity materialization")]
public class UserProfile
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public string Role { get; private set; } = "user";
    public string Tier { get; private set; } = "free";
    public string Status { get; private set; } = "Active";
    public DateTime CreatedAt { get; private set; }
    public int Level { get; private set; } = 1;
    public int ExperiencePoints { get; private set; }
    public bool EmailVerified { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public bool IsSuspended { get; private set; }
    public bool IsContributor { get; private set; }
    public bool IsDemoAccount { get; private set; }

    // EF Core requires a parameterless constructor for entity materialization
    internal UserProfile() { }
}
