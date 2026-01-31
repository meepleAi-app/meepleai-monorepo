namespace Api.Infrastructure.Configuration;

/// <summary>
/// Result of secret validation operation
/// </summary>
/// <remarks>
/// ISSUE-2510: Secrets management system with 3-level validation
/// - Critical: Must exist, startup blocked if missing
/// - Important: Should exist, warning logged if missing
/// - Optional: Nice to have, info logged if missing
/// </remarks>
internal sealed record SecretValidationResult
{
    /// <summary>
    /// Critical secrets that are missing (startup will fail)
    /// </summary>
    public List<string> MissingCritical { get; init; } = new();

    /// <summary>
    /// Important secrets that are missing (warnings logged)
    /// </summary>
    public List<string> MissingImportant { get; init; } = new();

    /// <summary>
    /// Optional secrets that are missing (info logged)
    /// </summary>
    public List<string> MissingOptional { get; init; } = new();

    /// <summary>
    /// Secrets that failed to parse (format errors)
    /// </summary>
    public Dictionary<string, string> ParseErrors { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Successfully loaded secrets (key = secret name)
    /// </summary>
    public HashSet<string> LoadedSecrets { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Indicates if validation passed (no critical secrets missing)
    /// </summary>
    public bool IsValid => MissingCritical.Count == 0;

    /// <summary>
    /// Indicates if there are any warnings (important secrets missing)
    /// </summary>
    public bool HasWarnings => MissingImportant.Count > 0 || ParseErrors.Count > 0;

    /// <summary>
    /// Total number of secrets processed
    /// </summary>
    public int TotalProcessed => LoadedSecrets.Count + MissingCritical.Count + MissingImportant.Count + MissingOptional.Count;
}
