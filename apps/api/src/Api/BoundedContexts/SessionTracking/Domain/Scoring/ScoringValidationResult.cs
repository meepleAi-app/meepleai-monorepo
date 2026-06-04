namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// Result of IScoringStrategy.Validate(). Encapsulates IsValid flag + error list.
/// Internal to Scoring namespace (no shared kernel dependency).
/// </summary>
public sealed record ScoringValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ScoringValidationResult Valid() => new(true, Array.Empty<string>());

    public static ScoringValidationResult Failed(string error) =>
        new(false, new[] { error });

    public static ScoringValidationResult Failed(IEnumerable<string> errors) =>
        new(false, errors.ToList().AsReadOnly());
}
