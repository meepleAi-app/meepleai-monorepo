using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Tests for the TierLimits value object.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TierLimitsTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidValues_ReturnsInstance()
    {
        // Act
        var limits = TierLimits.Create(
            maxPrivateGames: 5,
            maxPdfUploadsPerMonth: 10,
            maxPdfSizeBytes: 100L * 1024 * 1024,
            maxAgents: 3,
            maxAgentQueriesPerDay: 50,
            maxSessionQueries: 30,
            maxSessionPlayers: 6,
            maxPhotosPerSession: 10,
            sessionSaveEnabled: true,
            maxCatalogProposalsPerWeek: 2);

        // Assert
        limits.MaxPrivateGames.Should().Be(5);
        limits.MaxPdfUploadsPerMonth.Should().Be(10);
        limits.MaxPdfSizeBytes.Should().Be(100L * 1024 * 1024);
        limits.MaxAgents.Should().Be(3);
        limits.MaxAgentQueriesPerDay.Should().Be(50);
        limits.MaxSessionQueries.Should().Be(30);
        limits.MaxSessionPlayers.Should().Be(6);
        limits.MaxPhotosPerSession.Should().Be(10);
        limits.SessionSaveEnabled.Should().BeTrue();
        limits.MaxCatalogProposalsPerWeek.Should().Be(2);
    }

    [Fact]
    public void Create_WithZeroValues_ReturnsInstance()
    {
        // Act — zero is valid for most fields
        var limits = TierLimits.Create(0, 0, 0, 0, 0, 0, 1, 0, false, 0);

        // Assert
        limits.MaxPrivateGames.Should().Be(0);
        limits.MaxPdfUploadsPerMonth.Should().Be(0);
    }

    [Fact]
    public void Create_WithNegativeMaxPrivateGames_ThrowsArgumentException()
    {
        // Act
        var act = () => TierLimits.Create(-1, 0, 0, 0, 0, 0, 1, 0, false, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPrivateGames");
    }

    [Fact]
    public void Create_WithNegativeMaxPdfUploadsPerMonth_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, -1, 0, 0, 0, 0, 1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPdfUploadsPerMonth");
    }

    [Fact]
    public void Create_WithNegativeMaxPdfSizeBytes_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, -1, 0, 0, 0, 1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPdfSizeBytes");
    }

    [Fact]
    public void Create_WithNegativeMaxAgents_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, -1, 0, 0, 1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxAgents");
    }

    [Fact]
    public void Create_WithZeroMaxSessionPlayers_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, 0, 0, 0, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxSessionPlayers");
    }

    [Fact]
    public void Create_WithNegativeMaxSessionPlayers_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, 0, 0, -1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxSessionPlayers");
    }

    [Fact]
    public void Create_WithNegativeMaxAgentQueriesPerDay_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, -1, 0, 1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxAgentQueriesPerDay");
    }

    [Fact]
    public void Create_WithNegativeMaxSessionQueries_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, 0, -1, 1, 0, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxSessionQueries");
    }

    [Fact]
    public void Create_WithNegativeMaxPhotosPerSession_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, 0, 0, 1, -1, false, 0);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPhotosPerSession");
    }

    [Fact]
    public void Create_WithNegativeMaxCatalogProposalsPerWeek_ThrowsArgumentException()
    {
        var act = () => TierLimits.Create(0, 0, 0, 0, 0, 0, 1, 0, false, -1);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxCatalogProposalsPerWeek");
    }

    #endregion

    #region Preset Tests

    [Fact]
    public void FreeTier_HasCorrectDefaults()
    {
        var limits = TierLimits.FreeTier;

        limits.MaxPrivateGames.Should().Be(3);
        limits.MaxPdfUploadsPerMonth.Should().Be(3);
        limits.MaxPdfSizeBytes.Should().Be(50L * 1024 * 1024);
        limits.MaxAgents.Should().Be(1);
        limits.MaxAgentQueriesPerDay.Should().Be(20);
        limits.MaxSessionQueries.Should().Be(30);
        limits.MaxSessionPlayers.Should().Be(6);
        limits.MaxPhotosPerSession.Should().Be(5);
        limits.SessionSaveEnabled.Should().BeFalse();
        limits.MaxCatalogProposalsPerWeek.Should().Be(1);
    }

    [Fact]
    public void PremiumTier_HasCorrectDefaults()
    {
        var limits = TierLimits.PremiumTier;

        limits.MaxPrivateGames.Should().Be(15);
        limits.MaxPdfUploadsPerMonth.Should().Be(15);
        limits.MaxPdfSizeBytes.Should().Be(200L * 1024 * 1024);
        limits.MaxAgents.Should().Be(10);
        limits.MaxAgentQueriesPerDay.Should().Be(200);
        limits.MaxSessionQueries.Should().Be(150);
        limits.MaxSessionPlayers.Should().Be(12);
        limits.MaxPhotosPerSession.Should().Be(20);
        limits.SessionSaveEnabled.Should().BeTrue();
        limits.MaxCatalogProposalsPerWeek.Should().Be(5);
    }

    [Fact]
    public void Unlimited_HasMaxValueFields()
    {
        var limits = TierLimits.Unlimited;

        limits.MaxPrivateGames.Should().Be(int.MaxValue);
        limits.MaxPdfUploadsPerMonth.Should().Be(int.MaxValue);
        limits.MaxPdfSizeBytes.Should().Be(500L * 1024 * 1024);
        limits.MaxAgents.Should().Be(int.MaxValue);
        limits.MaxAgentQueriesPerDay.Should().Be(int.MaxValue);
        limits.MaxSessionQueries.Should().Be(int.MaxValue);
        limits.MaxSessionPlayers.Should().Be(12);
        limits.MaxPhotosPerSession.Should().Be(int.MaxValue);
        limits.SessionSaveEnabled.Should().BeTrue();
        limits.MaxCatalogProposalsPerWeek.Should().Be(int.MaxValue);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoLimits_WithSameValues_AreEqual()
    {
        var a = TierLimits.Create(3, 3, 50L * 1024 * 1024, 1, 20, 30, 6, 5, false, 1);
        var b = TierLimits.Create(3, 3, 50L * 1024 * 1024, 1, 20, 30, 6, 5, false, 1);

        a.Should().Be(b);
    }

    [Fact]
    public void TwoLimits_WithDifferentValues_AreNotEqual()
    {
        var a = TierLimits.FreeTier;
        var b = TierLimits.PremiumTier;

        a.Should().NotBe(b);
    }

    #endregion
}
