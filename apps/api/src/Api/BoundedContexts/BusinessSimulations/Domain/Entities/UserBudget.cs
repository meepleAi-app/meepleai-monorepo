using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Read-only projection of user budget/tier data from the users table.
/// Used by BusinessSimulations BC queries for cost scenarios and resource forecasting
/// without loading the full User aggregate.
/// Maps to the same "users" table via ToView() — EF treats it as read-only (no migrations).
/// </summary>
[SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Private setters are required by EF Core for entity materialization")]
public class UserBudget
{
    public Guid Id { get; private set; }
    public string Tier { get; private set; } = "free";
    public int Level { get; private set; } = 1;
    public int ExperiencePoints { get; private set; }
    public bool IsContributor { get; private set; }

    // EF Core requires a parameterless constructor for entity materialization
    internal UserBudget() { }
}
