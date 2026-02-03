using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Aggregate root representing a reusable agent typology template with predefined behavior.
/// Agent typologies define agent archetypes (Rules Expert, Quick Start, Ledger Master)
/// with associated prompt templates and default strategies.
/// Issue #3175
/// </summary>
internal sealed class AgentTypology : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string BasePrompt { get; private set; }
    public AgentStrategy DefaultStrategy { get; private set; }
    public TypologyStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private AgentTypology() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new agent typology with validation.
    /// </summary>
    public AgentTypology(
        Guid id,
        string name,
        string description,
        string basePrompt,
        AgentStrategy defaultStrategy,
        Guid createdBy,
        TypologyStatus status = TypologyStatus.Draft) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Typology name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (string.IsNullOrWhiteSpace(basePrompt))
            throw new ArgumentException("Base prompt cannot be empty", nameof(basePrompt));

        ArgumentNullException.ThrowIfNull(defaultStrategy);

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        Name = name.Trim();
        Description = description.Trim();
        BasePrompt = basePrompt.Trim();
        DefaultStrategy = defaultStrategy;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// Approves the typology for production use.
    /// </summary>
    public void Approve(Guid approvedBy)
    {
        if (approvedBy == Guid.Empty)
            throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

        if (Status == TypologyStatus.Approved)
            return;

        Status = TypologyStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rejects the typology.
    /// </summary>
    public void Reject(Guid rejectedBy)
    {
        if (rejectedBy == Guid.Empty)
            throw new ArgumentException("RejectedBy cannot be empty", nameof(rejectedBy));

        if (Status == TypologyStatus.Rejected)
            return;

        Status = TypologyStatus.Rejected;
        ApprovedBy = rejectedBy;
        ApprovedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Submits the typology for approval.
    /// </summary>
    public void SubmitForApproval()
    {
        if (Status != TypologyStatus.Draft)
            throw new InvalidOperationException("Only draft typologies can be submitted for approval");

        Status = TypologyStatus.Pending;
    }

    /// <summary>
    /// Updates the typology configuration.
    /// </summary>
    public void Update(
        string name,
        string description,
        string basePrompt,
        AgentStrategy defaultStrategy)
    {
        if (Status == TypologyStatus.Approved || Status == TypologyStatus.Rejected)
            throw new InvalidOperationException("Cannot update approved or rejected typologies");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Typology name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (string.IsNullOrWhiteSpace(basePrompt))
            throw new ArgumentException("Base prompt cannot be empty", nameof(basePrompt));

        ArgumentNullException.ThrowIfNull(defaultStrategy);

        Name = name.Trim();
        Description = description.Trim();
        BasePrompt = basePrompt.Trim();
        DefaultStrategy = defaultStrategy;
    }

    /// <summary>
    /// Soft deletes the typology.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
    }

    /// <summary>
    /// Restores a soft-deleted typology.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted)
            return;

        IsDeleted = false;
    }

    /// <summary>
    /// Checks if the typology is available for use.
    /// </summary>
    public bool IsAvailable => Status == TypologyStatus.Approved && !IsDeleted;
}
