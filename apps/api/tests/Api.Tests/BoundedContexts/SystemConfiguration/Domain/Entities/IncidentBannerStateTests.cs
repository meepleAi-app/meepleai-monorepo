using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Issue #1089: Unit tests for the IncidentBannerState domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class IncidentBannerStateTests
{
    [Fact]
    public void Create_WithValidArguments_SetsAllFields()
    {
        var state = IncidentBannerState.Create(
            message: "Scheduled maintenance",
            severity: BannerSeverity.Warning,
            isActive: true,
            startsAt: null,
            endsAt: null,
            updatedBy: "admin@example.com");

        state.Id.Should().Be(IncidentBannerState.SingletonId);
        state.Message.Should().Be("Scheduled maintenance");
        state.Severity.Should().Be(BannerSeverity.Warning);
        state.IsActive.Should().BeTrue();
        state.UpdatedBy.Should().Be("admin@example.com");
    }

    [Fact]
    public void Create_ActiveWithEmptyMessage_Throws()
    {
        var act = () => IncidentBannerState.Create(
            message: " ",
            severity: BannerSeverity.Info,
            isActive: true,
            startsAt: null,
            endsAt: null,
            updatedBy: null);

        act.Should().Throw<ArgumentException>().WithMessage("*Message is required*");
    }

    [Fact]
    public void Create_MessageExceedsMaxLength_Throws()
    {
        var act = () => IncidentBannerState.Create(
            message: new string('x', IncidentBannerState.MaxMessageLength + 1),
            severity: BannerSeverity.Info,
            isActive: false,
            startsAt: null,
            endsAt: null,
            updatedBy: null);

        act.Should().Throw<ArgumentException>().WithMessage("*exceed*");
    }

    [Fact]
    public void Create_StartsAtAfterEndsAt_Throws()
    {
        var now = DateTime.UtcNow;
        var act = () => IncidentBannerState.Create(
            message: "x",
            severity: BannerSeverity.Info,
            isActive: false,
            startsAt: now.AddHours(2),
            endsAt: now.AddHours(1),
            updatedBy: null);

        act.Should().Throw<ArgumentException>().WithMessage("*earlier than EndsAt*");
    }

    [Fact]
    public void IsCurrentlyVisible_Inactive_ReturnsFalse()
    {
        var state = IncidentBannerState.Create("x", BannerSeverity.Info, false, null, null, null);
        state.IsCurrentlyVisible(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyVisible_ActiveWithoutWindow_ReturnsTrue()
    {
        var state = IncidentBannerState.Create("hello", BannerSeverity.Info, true, null, null, null);
        state.IsCurrentlyVisible(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyVisible_BeforeStartsAt_ReturnsFalse()
    {
        var now = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        var state = IncidentBannerState.Create(
            "x", BannerSeverity.Info, true, startsAt: now.AddHours(1), endsAt: null, updatedBy: null);
        state.IsCurrentlyVisible(now).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyVisible_AfterEndsAt_ReturnsFalse()
    {
        var now = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        var state = IncidentBannerState.Create(
            "x", BannerSeverity.Info, true, startsAt: null, endsAt: now.AddMinutes(-1), updatedBy: null);
        state.IsCurrentlyVisible(now).Should().BeFalse();
    }

    [Fact]
    public void IsCurrentlyVisible_WithinWindow_ReturnsTrue()
    {
        var now = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        var state = IncidentBannerState.Create(
            "x", BannerSeverity.Critical, true,
            startsAt: now.AddHours(-1), endsAt: now.AddHours(1), updatedBy: null);
        state.IsCurrentlyVisible(now).Should().BeTrue();
    }

    [Fact]
    public void Update_MutatesFieldsAndAdvancesUpdatedAt()
    {
        var state = IncidentBannerState.Create("old", BannerSeverity.Info, false, null, null, null);
        var originalUpdatedAt = state.UpdatedAt;

        // Sleep to ensure UpdatedAt advances even on fast clocks.
        Thread.Sleep(2);

        state.Update("new", BannerSeverity.Critical, true, null, null, "admin");

        state.Message.Should().Be("new");
        state.Severity.Should().Be(BannerSeverity.Critical);
        state.IsActive.Should().BeTrue();
        state.UpdatedBy.Should().Be("admin");
        state.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }
}
