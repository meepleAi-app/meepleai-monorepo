using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Aggregate root for an A/B test session — a blind model comparison.
/// An admin/editor submits a query, multiple models respond blindly,
/// and the evaluator scores each variant without knowing which model produced it.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestSession : AggregateRoot<Guid>
{
    public Guid CreatedBy { get; private set; }
    public string Query { get; private set; } = default!;
    public Guid? KnowledgeBaseId { get; private set; }
    public AbTestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// </summary>
    public byte[] RowVersion { get; private set; } = default!;

    private readonly List<AbTestVariant> _variants = new();
    public IReadOnlyList<AbTestVariant> Variants => _variants.AsReadOnly();

#pragma warning disable CS8618
    private AbTestSession() : base() { } // EF Core
#pragma warning restore CS8618

    private AbTestSession(Guid id, Guid createdBy, string query, Guid? knowledgeBaseId)
        : base(id)
    {
        CreatedBy = createdBy;
        Query = query;
        KnowledgeBaseId = knowledgeBaseId;
        Status = AbTestStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new A/B test session.
    /// </summary>
    /// <param name="createdBy">The user who created the test.</param>
    /// <param name="query">The prompt to send to all models (max 2000 chars).</param>
    /// <param name="knowledgeBaseId">Optional KB context for RAG-based comparisons.</param>
    public static AbTestSession Create(Guid createdBy, string query, Guid? knowledgeBaseId = null)
    {
        if (createdBy == Guid.Empty)
            throw new ValidationException(nameof(AbTestSession), "CreatedBy is required");

        if (string.IsNullOrWhiteSpace(query))
            throw new ValidationException(nameof(AbTestSession), "Query is required");

        var trimmed = query.Trim();
        if (trimmed.Length > 2000)
            throw new ValidationException(nameof(AbTestSession), "Query must be 2000 characters or less");

        return new AbTestSession(Guid.NewGuid(), createdBy, trimmed, knowledgeBaseId);
    }

    /// <summary>
    /// Adds a variant (model) to the test session.
    /// Must be in Draft status. Supports 2-4 variants.
    /// </summary>
    public AbTestVariant AddVariant(string label, string provider, string modelId)
    {
        if (Status != AbTestStatus.Draft)
            throw new InvalidOperationException("Can only add variants to a Draft session");

        if (_variants.Count >= 4)
            throw new ValidationException(nameof(AbTestSession), "Maximum 4 variants per test session");

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required", nameof(label));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required", nameof(provider));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("ModelId is required", nameof(modelId));

        if (_variants.Any(v => string.Equals(v.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException(nameof(AbTestSession), $"Variant with label '{label}' already exists");

        var variant = new AbTestVariant(Guid.NewGuid(), Id, label.Trim(), provider.Trim(), modelId.Trim());
        _variants.Add(variant);
        return variant;
    }

    /// <summary>
    /// Transitions from Draft to InProgress once responses are being generated.
    /// Requires at least 2 variants.
    /// </summary>
    public void StartTest()
    {
        if (Status != AbTestStatus.Draft)
            throw new InvalidOperationException("Can only start a Draft session");

        if (_variants.Count < 2)
            throw new ValidationException(nameof(AbTestSession), "At least 2 variants are required to start a test");

        Status = AbTestStatus.InProgress;
    }

    /// <summary>
    /// Evaluates a specific variant by label. Only allowed when InProgress.
    /// </summary>
    public void EvaluateVariant(string label, AbTestEvaluation evaluation)
    {
        if (Status != AbTestStatus.InProgress)
            throw new InvalidOperationException("Can only evaluate variants in an InProgress session");

        var variant = _variants.FirstOrDefault(v =>
            string.Equals(v.Label, label, StringComparison.OrdinalIgnoreCase));

        if (variant is null)
            throw new ValidationException(nameof(AbTestSession), $"Variant '{label}' not found");

        variant.Evaluate(evaluation);

        // Auto-complete if all non-failed variants are evaluated
        if (_variants.Where(v => !v.Failed).All(v => v.Evaluation is not null))
        {
            Status = AbTestStatus.Evaluated;
            CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets the winning variant (highest average evaluation score).
    /// Only available after evaluation is complete.
    /// </summary>
    public AbTestVariant? GetWinner()
    {
        if (Status != AbTestStatus.Evaluated)
            return null;

        return _variants
            .Where(v => v.Evaluation is not null)
            .OrderByDescending(v => v.Evaluation!.AverageScore)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the total cost of all variants in this session.
    /// </summary>
    public decimal TotalCost => _variants.Sum(v => v.CostUsd);
}
