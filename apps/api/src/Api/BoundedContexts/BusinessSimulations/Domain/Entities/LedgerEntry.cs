using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Financial ledger entry representing an income or expense transaction.
/// Issue #3720: Financial Ledger Data Model (Epic #3688: Business and Simulations)
/// </summary>
public sealed class LedgerEntry : AggregateRoot<Guid>
{
    /// <summary>Date when the transaction occurred</summary>
    public DateTime Date { get; private set; }

    /// <summary>Income or Expense</summary>
    public LedgerEntryType Type { get; private set; }

    /// <summary>Transaction category (Subscription, TokenPurchase, etc.)</summary>
    public LedgerCategory Category { get; private set; }

    /// <summary>Monetary amount with currency</summary>
    public Money Amount { get; private set; }

    /// <summary>How this entry was created (Auto or Manual)</summary>
    public LedgerEntrySource Source { get; private set; }

    /// <summary>Optional description of the transaction</summary>
    public string? Description { get; private set; }

    /// <summary>Optional JSON metadata for additional context</summary>
    public string? Metadata { get; private set; }

    /// <summary>User who created this entry (null for Auto-generated entries)</summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>When this entry was created</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>When this entry was last updated (null if never updated)</summary>
    public DateTime? UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value
    private LedgerEntry() : base()
#pragma warning restore CS8618
    {
    }

    private LedgerEntry(
        Guid id,
        DateTime date,
        LedgerEntryType type,
        LedgerCategory category,
        Money amount,
        LedgerEntrySource source,
        string? description,
        string? metadata,
        Guid? createdByUserId) : base(id)
    {
        Date = date;
        Type = type;
        Category = category;
        Amount = amount;
        Source = source;
        Description = description;
        Metadata = metadata;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new ledger entry with full validation.
    /// </summary>
    public static LedgerEntry Create(
        DateTime date,
        LedgerEntryType type,
        LedgerCategory category,
        Money amount,
        LedgerEntrySource source,
        string? description = null,
        string? metadata = null,
        Guid? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (date > DateTime.UtcNow.AddDays(1))
            throw new ArgumentException("Ledger entry date cannot be in the future", nameof(date));

        if (amount.Amount == 0)
            throw new ArgumentException("Ledger entry amount must be greater than zero", nameof(amount));

        if (description is { Length: > 500 })
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        if (metadata is { Length: > 4000 })
            throw new ArgumentException("Metadata cannot exceed 4000 characters", nameof(metadata));

        if (source == LedgerEntrySource.Manual && createdByUserId == null)
            throw new ArgumentException("Manual entries must have a CreatedByUserId", nameof(createdByUserId));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty", nameof(createdByUserId));

        return new LedgerEntry(
            Guid.NewGuid(),
            date,
            type,
            category,
            amount,
            source,
            description?.Trim(),
            metadata,
            createdByUserId);
    }

    /// <summary>
    /// Creates an automatic system-generated ledger entry.
    /// </summary>
    public static LedgerEntry CreateAutoEntry(
        DateTime date,
        LedgerEntryType type,
        LedgerCategory category,
        decimal amount,
        string currency = "EUR",
        string? description = null,
        string? metadata = null)
    {
        return Create(
            date,
            type,
            category,
            Money.Create(amount, currency),
            LedgerEntrySource.Auto,
            description,
            metadata,
            createdByUserId: null);
    }

    /// <summary>
    /// Creates a manually entered ledger entry by an administrator.
    /// </summary>
    public static LedgerEntry CreateManualEntry(
        DateTime date,
        LedgerEntryType type,
        LedgerCategory category,
        decimal amount,
        Guid createdByUserId,
        string currency = "EUR",
        string? description = null,
        string? metadata = null)
    {
        return Create(
            date,
            type,
            category,
            Money.Create(amount, currency),
            LedgerEntrySource.Manual,
            description,
            metadata,
            createdByUserId);
    }

    /// <summary>
    /// Updates the description of this ledger entry.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        if (description is { Length: > 500 })
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the metadata of this ledger entry.
    /// </summary>
    public void UpdateMetadata(string? metadata)
    {
        if (metadata is { Length: > 4000 })
            throw new ArgumentException("Metadata cannot exceed 4000 characters", nameof(metadata));

        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the category of this ledger entry.
    /// </summary>
    public void UpdateCategory(LedgerCategory category)
    {
        Category = category;
        UpdatedAt = DateTime.UtcNow;
    }
}
