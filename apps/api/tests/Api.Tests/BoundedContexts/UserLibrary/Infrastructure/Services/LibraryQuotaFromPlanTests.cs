using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;
using UserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure.Services;

/// <summary>
/// Issue #4212: the library limit lives in the plan (<see cref="TierLimits.MaxLibraryGames"/>),
/// edited at <c>/admin/config/tiers</c>. Before, <see cref="GameLibraryQuotaService"/> read the
/// <c>LibraryLimits:{tier}:MaxGames</c> configuration keys, while the admin page wrote
/// <c>GameLibrary:*TierLimit</c> — different keys, so no admin change ever reached the quota.
/// The services here are the real ones (plan resolution, cache, update handler); only the
/// library count and Redis are doubles.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class LibraryQuotaFromPlanTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly MemoryCache _cache;
    private readonly Mock<IUserLibraryRepository> _library;
    private readonly GameLibraryQuotaService _quota;
    private readonly UpdateTierDefinitionCommandHandler _updateTier;

    public LibraryQuotaFromPlanTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _library = new Mock<IUserLibraryRepository>();

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(new Mock<IDatabase>().Object);
        var tiers = new TierEnforcementService(
            redis.Object, _db, _cache, NullLogger<TierEnforcementService>.Instance);

        _quota = new GameLibraryQuotaService(
            _library.Object, tiers, NullLogger<GameLibraryQuotaService>.Instance);
        _updateTier = new UpdateTierDefinitionCommandHandler(_db, _cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task TheLibraryLimit_ComesFromTheUsersPlan()
    {
        var userId = await SeedFreeUserAsync();
        await SeedPlanAsync("free", TierLimits.FreeTier with { MaxLibraryGames = 2 });
        LibraryHas(userId, games: 2);

        var result = await _quota.CheckQuotaAsync(
            userId, UserTier.Free, AuthRole.User, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeFalse("the plan allows 2 games and the library already has 2");
        result.MaxAllowed.Should().Be(2);
    }

    [Fact]
    public async Task ChangingTheLimitInThePlan_ChangesTheQuotaRightAway()
    {
        var userId = await SeedFreeUserAsync();
        await SeedPlanAsync("free", TierLimits.FreeTier with { MaxLibraryGames = 2 });
        LibraryHas(userId, games: 2);

        // Read once, so the plan is in the cache before the admin edits it.
        var before = await _quota.CheckQuotaAsync(
            userId, UserTier.Free, AuthRole.User, TestContext.Current.CancellationToken);
        before.IsAllowed.Should().BeFalse();

        var raised = TierLimitsDto.FromValueObject(TierLimits.FreeTier with { MaxLibraryGames = 3 });
        await _updateTier.Handle(
            new UpdateTierDefinitionCommand("free", null, raised, null, null),
            TestContext.Current.CancellationToken);

        var after = await _quota.CheckQuotaAsync(
            userId, UserTier.Free, AuthRole.User, TestContext.Current.CancellationToken);

        after.IsAllowed.Should().BeTrue(
            "the admin raised the plan to 3 games; a cached plan would still say 2");
        after.MaxAllowed.Should().Be(3);
    }

    [Fact]
    public async Task QuotaInfo_ShowsTheLimitOfThePlan()
    {
        var userId = await SeedFreeUserAsync();
        await SeedPlanAsync("free", TierLimits.FreeTier with { MaxLibraryGames = 7 });
        LibraryHas(userId, games: 4);

        var info = await _quota.GetQuotaInfoAsync(
            userId, UserTier.Free, AuthRole.User, TestContext.Current.CancellationToken);

        info.MaxGames.Should().Be(7);
        info.RemainingSlots.Should().Be(3);
        info.IsUnlimited.Should().BeFalse();
    }

    private async Task<Guid> SeedFreeUserAsync()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userId;
    }

    private async Task SeedPlanAsync(string name, TierLimits limits)
    {
        _db.TierDefinitions.Add(TierDefinition.Create(name, name, limits, "free"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private void LibraryHas(Guid userId, int games) =>
        _library.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(games);
}
