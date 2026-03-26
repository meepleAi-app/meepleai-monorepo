using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the AI arbitrator's structured verdict on a rule dispute.
/// </summary>
internal sealed record DisputeVerdict
{
    /// <summary>Which party the ruling favors.</summary>
    public RulingFor RulingFor { get; init; }

    /// <summary>The AI's reasoning for the ruling.</summary>
    public string Reasoning { get; init; }

    /// <summary>Optional citation from the rulebook supporting the verdict.</summary>
    public string? Citation { get; init; }

    /// <summary>How confident the AI is in this ruling.</summary>
    public VerdictConfidence Confidence { get; init; }

    public DisputeVerdict(RulingFor rulingFor, string reasoning, string? citation, VerdictConfidence confidence)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
            throw new ArgumentException("Reasoning is required.", nameof(reasoning));

        RulingFor = rulingFor;
        Reasoning = reasoning;
        Citation = citation;
        Confidence = confidence;
    }
}
