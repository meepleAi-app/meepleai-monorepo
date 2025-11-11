namespace Api.Models;

/// <summary>
/// Represents the difference between two RuleSpec versions
/// </summary>
public record RuleSpecDiff(
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
public record DiffSummary(
    int TotalChanges,
    int Added,
    int Modified,
    int Deleted,
    int Unchanged
);

/// <summary>
/// Represents a change to a single rule atom
/// </summary>
public record RuleAtomChange(
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
public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Unchanged
}

/// <summary>
/// Represents a change to a specific field within a rule atom
/// </summary>
public record FieldChange(
    string FieldName,
    string? OldValue,
    string? NewValue
);

/// <summary>
/// Represents the version history of a RuleSpec
/// </summary>
public record RuleSpecVersion(
    string Version,
    DateTime CreatedAt,
    int RuleCount,
    string? CreatedBy = null
);

/// <summary>
/// Represents the history of changes to a RuleSpec
/// </summary>
public record RuleSpecHistory(
    string GameId,
    IReadOnlyList<RuleSpecVersion> Versions,
    int TotalVersions
);
