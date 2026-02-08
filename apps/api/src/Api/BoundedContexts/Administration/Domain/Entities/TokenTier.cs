using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Aggregate Root representing a subscription tier configuration (Issue #3692)
/// </summary>
public sealed class TokenTier
{
    public Guid Id { get; private set; }
    public TierName Name { get; private set; }
    public TierLimits Limits { get; private set; }
    public TierPricing Pricing { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // EF Core
    private TokenTier()
    {
        Limits = null!;
        Pricing = null!;
    }

    private TokenTier(Guid id, TierName name, TierLimits limits, TierPricing pricing)
    {
        Id = id;
        Name = name;
        Limits = limits;
        Pricing = pricing;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new TokenTier
    /// </summary>
    public static TokenTier Create(TierName name, TierLimits limits, TierPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(pricing);

        return new TokenTier(Guid.NewGuid(), name, limits, pricing);
    }

    /// <summary>
    /// Update tier limits (Admin action)
    /// </summary>
    public void UpdateLimits(TierLimits newLimits)
    {
        ArgumentNullException.ThrowIfNull(newLimits);

        Limits = newLimits;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update tier pricing (Admin action)
    /// </summary>
    public void UpdatePricing(TierPricing newPricing)
    {
        ArgumentNullException.ThrowIfNull(newPricing);

        Pricing = newPricing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivate tier (soft delete)
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory: Create Free tier with default values
    /// </summary>
    public static TokenTier CreateFreeTier() => Create(TierName.Free, TierLimits.FreeTier(), TierPricing.FreeTier());

    /// <summary>
    /// Factory: Create Basic tier with default values
    /// </summary>
    public static TokenTier CreateBasicTier() => Create(TierName.Basic, TierLimits.BasicTier(), TierPricing.BasicTier());

    /// <summary>
    /// Factory: Create Pro tier with default values
    /// </summary>
    public static TokenTier CreateProTier() => Create(TierName.Pro, TierLimits.ProTier(), TierPricing.ProTier());

    /// <summary>
    /// Factory: Create Enterprise tier with default values
    /// </summary>
    public static TokenTier CreateEnterpriseTier() => Create(TierName.Enterprise, TierLimits.EnterpriseTier(), TierPricing.EnterpriseTier());
}