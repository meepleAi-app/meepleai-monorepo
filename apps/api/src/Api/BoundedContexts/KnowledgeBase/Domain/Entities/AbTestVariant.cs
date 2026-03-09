using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// A single variant (model response) within an A/B test session.
/// Contains the model's response, cost data, and optional evaluation.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestVariant : Entity<Guid>
{
    public Guid AbTestSessionId { get; private set; }
    public string Label { get; private set; } = default!;
    public string Provider { get; private set; } = default!;
    public string ModelId { get; private set; } = default!;
    public string? Response { get; private set; }
    public int TokensUsed { get; private set; }
    public int LatencyMs { get; private set; }
    public decimal CostUsd { get; private set; }
    public bool Failed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Evaluation scores assigned by an evaluator (null until evaluated).
    /// Stored as owned entity (OwnsOne) in the same table.
    /// </summary>
    public AbTestEvaluation? Evaluation { get; private set; }

#pragma warning disable CS8618
    private AbTestVariant() : base() { } // EF Core
#pragma warning restore CS8618

    internal AbTestVariant(
        Guid id,
        Guid sessionId,
        string label,
        string provider,
        string modelId) : base(id)
    {
        AbTestSessionId = sessionId;
        Label = label;
        Provider = provider;
        ModelId = modelId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the model's response after generation completes.
    /// </summary>
    public void RecordResponse(string response, int tokensUsed, int latencyMs, decimal costUsd)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be empty", nameof(response));

        if (tokensUsed < 0)
            throw new ArgumentException("TokensUsed cannot be negative", nameof(tokensUsed));

        Response = response;
        TokensUsed = tokensUsed;
        LatencyMs = latencyMs;
        CostUsd = costUsd;
    }

    /// <summary>
    /// Marks this variant as failed (e.g. model timeout).
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        Failed = true;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Assigns evaluation scores to this variant.
    /// </summary>
    public void Evaluate(AbTestEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (Evaluation is not null)
            throw new InvalidOperationException("Variant has already been evaluated");

        if (Response is null && !Failed)
            throw new InvalidOperationException("Cannot evaluate a variant without a response");

        Evaluation = evaluation;
    }
}
