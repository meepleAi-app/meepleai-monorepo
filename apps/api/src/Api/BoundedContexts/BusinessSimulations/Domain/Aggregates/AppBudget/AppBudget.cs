using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets;

/// <summary>
/// AppBudget aggregate root — singleton global budget configuration
/// (Issue #1838 SP5 F4-C5).
///
/// <para>There is at most one AppBudget row per environment. The aggregate
/// carries the monthly limit (a <see cref="Money"/> value object) plus the
/// alert/critical threshold percentages surfaced by the admin Business page
/// KPI strip and gauge.</para>
///
/// <para>Singleton enforcement is application-level: the repository's
/// <c>GetCurrentAsync</c>/<c>UpsertAsync</c> pair detects existing rows and
/// upgrades writes to in-place updates. We deliberately avoid a DB-level
/// constraint so the schema stays portable and future-friendly (e.g. multi-
/// tenant scoping could add a tenant discriminator without redoing migrations).</para>
///
/// <para>Xmin enables optimistic-concurrency protection: two simultaneous
/// admins editing the budget will surface a 409 ConflictException via
/// <c>DbUpdateConcurrencyException</c> translation in the upsert command handler.</para>
/// </summary>
internal sealed class AppBudget
{
    public Guid Id { get; private set; }

    /// <summary>Monthly spend limit as a Money value object (currency + amount).</summary>
    public Money MonthlyLimit { get; private set; } = Money.Zero();

    /// <summary>Warning threshold as percentage of the limit (default 80).</summary>
    public int AlertThresholdPct { get; private set; }

    /// <summary>Critical threshold as percentage of the limit (default 95).</summary>
    public int CriticalThresholdPct { get; private set; }

    /// <summary>Master switch — disable to silence alerts and gauges without losing config.</summary>
    public bool IsEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Postgres xmin optimistic-concurrency token. Server-owned; the repository
    /// round-trips it for detached update (ADR-060).</summary>
    public uint Xmin { get; private set; }

    /// <summary>Repository-only: restore the xmin token after loading from persistence.</summary>
    internal void SetXmin(uint xmin) => Xmin = xmin;

    private AppBudget() { /* EF Core / reconstitution */ }

    private AppBudget(
        Guid id,
        Money monthlyLimit,
        int alertThresholdPct,
        int criticalThresholdPct,
        bool isEnabled,
        DateTime createdAt,
        DateTime updatedAt,
        string? createdBy,
        string? updatedBy)
    {
        Id = id;
        MonthlyLimit = monthlyLimit;
        AlertThresholdPct = alertThresholdPct;
        CriticalThresholdPct = criticalThresholdPct;
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedBy = createdBy;
        UpdatedBy = updatedBy;
    }

    /// <summary>Creates a brand-new AppBudget singleton. The caller's identity
    /// becomes both <see cref="CreatedBy"/> and the initial <see cref="UpdatedBy"/>.</summary>
    public static AppBudget Create(
        Money monthlyLimit,
        int alertThresholdPct,
        int criticalThresholdPct,
        string createdBy)
    {
        ArgumentNullException.ThrowIfNull(monthlyLimit);
        ValidateThresholds(alertThresholdPct, criticalThresholdPct);
        ValidateLimit(monthlyLimit);
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("createdBy is required", nameof(createdBy));

        var now = DateTime.UtcNow;
        return new AppBudget(
            Guid.NewGuid(),
            monthlyLimit,
            alertThresholdPct,
            criticalThresholdPct,
            isEnabled: true,
            createdAt: now,
            updatedAt: now,
            createdBy: createdBy,
            updatedBy: createdBy);
    }

    /// <summary>Repository-only reconstitution; preserves stored xmin and timestamps.</summary>
    public static AppBudget Reconstitute(
        Guid id,
        Money monthlyLimit,
        int alertThresholdPct,
        int criticalThresholdPct,
        bool isEnabled,
        DateTime createdAt,
        DateTime updatedAt,
        string? createdBy,
        string? updatedBy,
        uint xmin)
    {
        ArgumentNullException.ThrowIfNull(monthlyLimit);
        return new AppBudget(
            id, monthlyLimit, alertThresholdPct, criticalThresholdPct, isEnabled,
            createdAt, updatedAt, createdBy, updatedBy)
        {
            Xmin = xmin,
        };
    }

    /// <summary>Replaces the monthly limit and threshold percentages (used by the upsert PUT endpoint).</summary>
    public void UpdateLimit(
        Money newLimit,
        int alertThresholdPct,
        int criticalThresholdPct,
        string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(newLimit);
        ValidateThresholds(alertThresholdPct, criticalThresholdPct);
        ValidateLimit(newLimit);
        if (string.IsNullOrWhiteSpace(updatedBy)) throw new ArgumentException("updatedBy is required", nameof(updatedBy));

        MonthlyLimit = newLimit;
        AlertThresholdPct = alertThresholdPct;
        CriticalThresholdPct = criticalThresholdPct;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Master switch ON: alerts and gauges resume.</summary>
    public void Enable(string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(updatedBy)) throw new ArgumentException("updatedBy is required", nameof(updatedBy));
        if (IsEnabled) return;

        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Master switch OFF: silences alerts and gauges without losing config.</summary>
    public void Disable(string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(updatedBy)) throw new ArgumentException("updatedBy is required", nameof(updatedBy));
        if (!IsEnabled) return;

        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    private static void ValidateThresholds(int alertPct, int criticalPct)
    {
        if (alertPct is < 1 or > 99)
            throw new ArgumentException("AlertThresholdPct must be between 1 and 99", nameof(alertPct));

        if (criticalPct is < 1 or > 100)
            throw new ArgumentException("CriticalThresholdPct must be between 1 and 100", nameof(criticalPct));

        if (criticalPct <= alertPct)
            throw new ArgumentException(
                "CriticalThresholdPct must be strictly greater than AlertThresholdPct",
                nameof(criticalPct));
    }

    private static void ValidateLimit(Money limit)
    {
        if (limit.Amount <= 0m)
            throw new ArgumentException("MonthlyLimit amount must be greater than zero", nameof(limit));
    }
}
