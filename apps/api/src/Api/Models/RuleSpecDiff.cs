

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

/// <summary>
/// Represents the difference between two RuleSpec versions
/// </summary>
internal record RuleSpecDiff(
    string GameId,
    string FromVersion,
    string ToVersion,
    DateTime FromCreatedAt,
    DateTime ToCreatedAt,
    DiffSummary Summary,
    IReadOnlyList<RuleAtomChange> Changes
);

/// <summary>
/// Summary of changes between two versions
/// </summary>
internal record DiffSummary(
    int TotalChanges,
    int Added,
    int Modified,
    int Deleted,
    int Unchanged
);

/// <summary>
/// Represents a change to a single rule atom
/// </summary>
internal record RuleAtomChange(
    ChangeType Type,
    string? OldAtom,
    string? NewAtom,
    RuleAtom? OldValue,
    RuleAtom? NewValue,
    IReadOnlyList<FieldChange>? FieldChanges = null
);

/// <summary>
/// Type of change for a rule atom
/// </summary>
internal enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Unchanged
}

/// <summary>
/// Represents a change to a specific field within a rule atom
/// </summary>
internal record FieldChange(
    string FieldName,
    string? OldValue,
    string? NewValue
);

/// <summary>
/// Represents the version history of a RuleSpec
/// </summary>
internal record RuleSpecVersion(
    string Version,
    DateTime CreatedAt,
    int RuleCount,
    string? CreatedBy = null
);

/// <summary>
/// Represents the history of changes to a RuleSpec
/// </summary>
internal record RuleSpecHistory(
    string GameId,
    IReadOnlyList<RuleSpecVersion> Versions,
    int TotalVersions
);
