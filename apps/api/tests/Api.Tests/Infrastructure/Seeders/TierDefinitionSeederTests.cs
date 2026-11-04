using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Issue #4212: the plans seeded at startup. Before, only free and premium existed, so a user on
/// the "normal" tier fell back to free for every plan limit. The library limit (20 for normal)
/// lived outside the plan and hid the gap; once it moves in, the normal plan has to exist.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class TierDefinitionSeederTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Seed_CreatesFreeNormalAndPremium_WithTheirLimits()
    {
        await TierDefinitionSeeder.SeedTierDefinitionsAsync(
            _db, NullLogger.Instance, TestContext.Current.CancellationToken);

        var plans = await _db.Set<TierDefinition>()
            .ToDictionaryAsync(t => t.Name, TestContext.Current.CancellationToken);

        plans.Keys.Should().BeEquivalentTo("free", "normal", "premium");
        plans["free"].Limits.Should().Be(TierLimits.FreeTier);
        plans["normal"].Limits.Should().Be(TierLimits.NormalTier);
        plans["premium"].Limits.Should().Be(TierLimits.PremiumTier);
        plans["free"].IsDefault.Should().BeTrue();
        plans["normal"].IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteAPlanTheAdminAlreadyEdited()
    {
        var edited = TierDefinition.Create("normal", "Normal", TierLimits.FreeTier with { MaxLibraryGames = 33 }, "free");
        _db.Set<TierDefinition>().Add(edited);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await TierDefinitionSeeder.SeedTierDefinitionsAsync(
            _db, NullLogger.Instance, TestContext.Current.CancellationToken);

        var normal = await _db.Set<TierDefinition>()
            .SingleAsync(t => t.Name == "normal", TestContext.Current.CancellationToken);
        normal.Limits.MaxLibraryGames.Should().Be(33);
    }
}
