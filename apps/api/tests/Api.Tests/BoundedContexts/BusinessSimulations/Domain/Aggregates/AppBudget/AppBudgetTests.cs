using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;
using AppBudgetAggregate = Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets.AppBudget;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudget;

/// <summary>
/// Unit tests for the AppBudget aggregate (Issue #1838 SP5 F4-C5).
/// Covers factory invariants (limit + thresholds), upsert lifecycle, master
/// switch behavior, and Reconstitute audit-field preservation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class AppBudgetTests
{
    private static Money UsdLimit(decimal amount) => Money.Create(amount, "USD");

    [Fact]
    public void Create_WithValidArguments_InitializesAggregate()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1200m), 80, 95, "admin@meepleai.dev");

        budget.Id.Should().NotBe(Guid.Empty);
        budget.MonthlyLimit.Amount.Should().Be(1200m);
        budget.MonthlyLimit.Currency.Should().Be("USD");
        budget.AlertThresholdPct.Should().Be(80);
        budget.CriticalThresholdPct.Should().Be(95);
        budget.IsEnabled.Should().BeTrue();
        budget.CreatedBy.Should().Be("admin@meepleai.dev");
        budget.UpdatedBy.Should().Be("admin@meepleai.dev");
        budget.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        budget.UpdatedAt.Should().Be(budget.CreatedAt);
        budget.RowVersion.Should().BeEmpty("Fresh aggregates have no DB-assigned token yet");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveLimit_Throws(decimal amount)
    {
        // Money.Create itself rejects negative; for amount == 0 the aggregate
        // factory's own guard must fire. Test both edges through the same call site.
        Action act = () => AppBudgetAggregate.Create(Money.Create(amount, "USD"), 80, 95, "admin");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 95)]   // alert at lower bound
    [InlineData(100, 95)] // alert above upper bound
    [InlineData(80, 0)]   // critical below lower bound
    [InlineData(80, 101)] // critical above upper bound
    public void Create_WithOutOfRangeThresholds_Throws(int alertPct, int criticalPct)
    {
        Action act = () => AppBudgetAggregate.Create(UsdLimit(1200m), alertPct, criticalPct, "admin");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(50, 50)] // alert == critical (must be strictly greater)
    [InlineData(80, 75)] // critical below alert
    public void Create_WhenCriticalNotStrictlyAboveAlert_Throws(int alertPct, int criticalPct)
    {
        Action act = () => AppBudgetAggregate.Create(UsdLimit(1200m), alertPct, criticalPct, "admin");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCreatedBy_Throws(string createdBy)
    {
        Action act = () => AppBudgetAggregate.Create(UsdLimit(1200m), 80, 95, createdBy);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateLimit_BumpsUpdatedAtAndUpdatedBy_PreservesCreatedAt()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1000m), 80, 95, "admin");
        var origCreated = budget.CreatedAt;

        // Force a measurable delta on the UtcNow clock so UpdatedAt != CreatedAt.
        System.Threading.Thread.Sleep(2);

        budget.UpdateLimit(UsdLimit(2000m), 70, 90, "admin2");

        budget.MonthlyLimit.Amount.Should().Be(2000m);
        budget.AlertThresholdPct.Should().Be(70);
        budget.CriticalThresholdPct.Should().Be(90);
        budget.UpdatedBy.Should().Be("admin2");
        budget.CreatedBy.Should().Be("admin", "CreatedBy is set once at Create() time");
        budget.CreatedAt.Should().Be(origCreated);
        budget.UpdatedAt.Should().BeOnOrAfter(origCreated);
    }

    [Fact]
    public void UpdateLimit_WithInvalidThresholds_Throws()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1000m), 80, 95, "admin");

        Action act = () => budget.UpdateLimit(UsdLimit(1200m), 90, 85, "admin");
        act.Should().Throw<ArgumentException>(
            "UpdateLimit must enforce the same thresholds invariants as Create");
    }

    [Fact]
    public void Disable_OnEnabledBudget_TogglesAndBumpsAudit()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1000m), 80, 95, "admin");
        System.Threading.Thread.Sleep(2);

        budget.Disable("admin2");

        budget.IsEnabled.Should().BeFalse();
        budget.UpdatedBy.Should().Be("admin2");
    }

    [Fact]
    public void Disable_OnAlreadyDisabledBudget_IsIdempotent()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1000m), 80, 95, "admin");
        budget.Disable("first");
        var snapshotUpdatedAt = budget.UpdatedAt;
        var snapshotUpdatedBy = budget.UpdatedBy;

        budget.Disable("second");

        budget.UpdatedAt.Should().Be(snapshotUpdatedAt, "Disable on a disabled budget is a no-op");
        budget.UpdatedBy.Should().Be(snapshotUpdatedBy);
    }

    [Fact]
    public void Enable_OnDisabledBudget_TogglesBack()
    {
        var budget = AppBudgetAggregate.Create(UsdLimit(1000m), 80, 95, "admin");
        budget.Disable("admin");
        System.Threading.Thread.Sleep(2);

        budget.Enable("admin2");

        budget.IsEnabled.Should().BeTrue();
        budget.UpdatedBy.Should().Be("admin2");
    }

    [Fact]
    public void Reconstitute_PreservesRowVersionAndAuditFields()
    {
        var id = Guid.NewGuid();
        var rowVersion = new byte[] { 9, 8, 7, 6 };
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

        var budget = AppBudgetAggregate.Reconstitute(
            id,
            UsdLimit(1500m),
            alertThresholdPct: 75,
            criticalThresholdPct: 90,
            isEnabled: false,
            createdAt: createdAt,
            updatedAt: updatedAt,
            createdBy: "founder@meepleai.dev",
            updatedBy: "ops@meepleai.dev",
            rowVersion: rowVersion);

        budget.Id.Should().Be(id);
        budget.MonthlyLimit.Amount.Should().Be(1500m);
        budget.AlertThresholdPct.Should().Be(75);
        budget.CriticalThresholdPct.Should().Be(90);
        budget.IsEnabled.Should().BeFalse();
        budget.CreatedAt.Should().Be(createdAt);
        budget.UpdatedAt.Should().Be(updatedAt);
        budget.CreatedBy.Should().Be("founder@meepleai.dev");
        budget.UpdatedBy.Should().Be("ops@meepleai.dev");
        budget.RowVersion.Should().BeEquivalentTo(rowVersion);
    }

    [Fact]
    public void Reconstitute_WithNullRowVersion_DefaultsToEmptyBytes()
    {
        var budget = AppBudgetAggregate.Reconstitute(
            Guid.NewGuid(),
            UsdLimit(1000m),
            80,
            95,
            isEnabled: true,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            createdBy: "x",
            updatedBy: "x",
            rowVersion: null!);

        budget.RowVersion.Should().NotBeNull();
        budget.RowVersion.Should().BeEmpty();
    }
}
