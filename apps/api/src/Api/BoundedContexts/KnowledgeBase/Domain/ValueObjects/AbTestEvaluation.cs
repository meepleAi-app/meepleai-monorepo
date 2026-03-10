using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Evaluation scores for an A/B test variant (1-5 per dimension).
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestEvaluation : ValueObject
{
    public Guid EvaluatorId { get; }
    public int Accuracy { get; }
    public int Completeness { get; }
    public int Clarity { get; }
    public int Tone { get; }
    public string? Notes { get; }
    public DateTime EvaluatedAt { get; }

    public decimal AverageScore => (Accuracy + Completeness + Clarity + Tone) / 4.0m;

    private AbTestEvaluation() { } // EF Core

    private AbTestEvaluation(Guid evaluatorId, int accuracy, int completeness, int clarity, int tone, string? notes)
    {
        EvaluatorId = evaluatorId;
        Accuracy = accuracy;
        Completeness = completeness;
        Clarity = clarity;
        Tone = tone;
        Notes = notes;
        EvaluatedAt = DateTime.UtcNow;
    }

    public static AbTestEvaluation Create(Guid evaluatorId, int accuracy, int completeness, int clarity, int tone, string? notes = null)
    {
        if (evaluatorId == Guid.Empty)
            throw new ValidationException(nameof(AbTestEvaluation), "EvaluatorId is required");

        ValidateScore(nameof(Accuracy), accuracy);
        ValidateScore(nameof(Completeness), completeness);
        ValidateScore(nameof(Clarity), clarity);
        ValidateScore(nameof(Tone), tone);

        if (notes is { Length: > 2000 })
            throw new ValidationException(nameof(AbTestEvaluation), "Notes must be 2000 characters or less");

        return new AbTestEvaluation(evaluatorId, accuracy, completeness, clarity, tone, notes);
    }

    private static void ValidateScore(string dimension, int value)
    {
        if (value is < 1 or > 5)
            throw new ValidationException(nameof(AbTestEvaluation), $"{dimension} must be between 1 and 5, got {value}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EvaluatorId;
        yield return Accuracy;
        yield return Completeness;
        yield return Clarity;
        yield return Tone;
        yield return Notes;
        yield return EvaluatedAt;
    }
}
