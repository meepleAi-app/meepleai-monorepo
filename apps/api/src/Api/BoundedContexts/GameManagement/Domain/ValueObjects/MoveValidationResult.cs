using Api.Models;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Result of move validation against game rules.
/// Immutable value object containing validation outcome and details.
/// </summary>
public sealed record MoveValidationResult
{
    /// <summary>
    /// Whether the move is valid according to the rules.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation error messages if the move is invalid.
    /// Empty if move is valid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Rules that were applied during validation.
    /// </summary>
    public IReadOnlyList<RuleAtom> ApplicableRules { get; init; }

    /// <summary>
    /// Confidence score of the validation (0.0-1.0).
    /// Higher values indicate more certain validation results.
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Optional suggestions for valid alternative moves.
    /// </summary>
    public IReadOnlyList<string>? Suggestions { get; init; }

    /// <summary>
    /// Creates a valid move result.
    /// </summary>
    public static MoveValidationResult Valid(
        IReadOnlyList<RuleAtom> applicableRules,
        double confidenceScore = 1.0,
        IReadOnlyList<string>? suggestions = null)
    {
        if (confidenceScore < 0.0 || confidenceScore > 1.0)
            throw new ArgumentException("Confidence score must be between 0.0 and 1.0", nameof(confidenceScore));

        return new MoveValidationResult
        {
            IsValid = true,
            Errors = Array.Empty<string>(),
            ApplicableRules = applicableRules ?? Array.Empty<RuleAtom>(),
            ConfidenceScore = confidenceScore,
            Suggestions = suggestions
        };
    }

    /// <summary>
    /// Creates an invalid move result with error messages.
    /// </summary>
    public static MoveValidationResult Invalid(
        IReadOnlyList<string> errors,
        IReadOnlyList<RuleAtom> applicableRules,
        double confidenceScore = 1.0,
        IReadOnlyList<string>? suggestions = null)
    {
        if (errors == null || errors.Count == 0)
            throw new ArgumentException("Invalid result must have at least one error", nameof(errors));

        if (confidenceScore < 0.0 || confidenceScore > 1.0)
            throw new ArgumentException("Confidence score must be between 0.0 and 1.0", nameof(confidenceScore));

        return new MoveValidationResult
        {
            IsValid = false,
            Errors = errors,
            ApplicableRules = applicableRules ?? Array.Empty<RuleAtom>(),
            ConfidenceScore = confidenceScore,
            Suggestions = suggestions
        };
    }

    /// <summary>
    /// Creates an uncertain result when validation confidence is low.
    /// </summary>
    public static MoveValidationResult Uncertain(
        string reason,
        IReadOnlyList<RuleAtom> applicableRules,
        double confidenceScore)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Uncertain result must have a reason", nameof(reason));

        if (confidenceScore < 0.0 || confidenceScore > 1.0)
            throw new ArgumentException("Confidence score must be between 0.0 and 1.0", nameof(confidenceScore));

        return new MoveValidationResult
        {
            IsValid = false,
            Errors = new[] { $"Uncertain: {reason}" },
            ApplicableRules = applicableRules ?? Array.Empty<RuleAtom>(),
            ConfidenceScore = confidenceScore,
            Suggestions = new[] { "Review game rules or consult rulebook" }
        };
    }
}
