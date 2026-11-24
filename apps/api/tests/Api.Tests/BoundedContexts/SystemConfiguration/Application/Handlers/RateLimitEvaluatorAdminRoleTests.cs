using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AuthUser = Api.BoundedContexts.Authentication.Domain.Entities.User;
using Role = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// #3994 — the share-request limits are lifted for an admin, because the admin tier is derived from the role. A
/// superadmin counts as an admin everywhere: the exact comparison with <c>"admin"</c> gave it the limits of its
/// subscription tier, free for the bootstrap account.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "3994")]
public sealed class RateLimitEvaluatorAdminRoleTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task GetUserStatusAsync_AdminOrSuperAdmin_HasNoLimits(string role)
    {
        var userId = Guid.NewGuid();

        var status = await Build(userId, Role.Parse(role)).GetUserStatusAsync(userId, TestContext.Current.CancellationToken);

        status.Tier.Should().Be(UserTier.Admin);
        status.EffectiveMaxPending.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task GetUserStatusAsync_Editor_KeepsTheLimitsOfItsTier()
    {
        var userId = Guid.NewGuid();

        var status = await Build(userId, Role.Editor).GetUserStatusAsync(userId, TestContext.Current.CancellationToken);

        status.Tier.Should().Be(UserTier.Free);
        status.EffectiveMaxPending.Should().Be(3);
    }

    /// <summary>A cache that always runs the factory: what is cached is not under test here.</summary>
    private sealed class PassThroughCache : IHybridCacheService
    {
        public Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class => factory(ct);

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static RateLimitEvaluator Build(Guid userId, Role role)
    {
        var user = AuthUser.CreateForOAuth(
            id: userId,
            email: new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email($"{role.Value}@meepleai.test"),
            displayName: "Prova",
            role: role,
            tier: null,
            oauthProvider: "google",
            timeProvider: TimeProvider.System);

        var configRepo = new Mock<IRateLimitConfigRepository>();
        configRepo
            .Setup(r => r.GetByTierAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareRequestLimitConfig.Create(
                tier: UserTier.Free,
                maxPendingRequests: 3,
                maxRequestsPerMonth: 10,
                cooldownAfterRejection: TimeSpan.FromDays(7)));

        var overrideRepo = new Mock<IUserRateLimitOverrideRepository>();
        overrideRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRateLimitOverride?)null);

        var shareRepo = new Mock<IShareRequestRepository>();
        shareRepo.Setup(r => r.CountPendingByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        shareRepo.Setup(r => r.CountThisMonthByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        shareRepo.Setup(r => r.GetLastRejectionDateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        return new RateLimitEvaluator(
            configRepo.Object,
            overrideRepo.Object,
            shareRepo.Object,
            userRepo.Object,
            new PassThroughCache(),
            NullLogger<RateLimitEvaluator>.Instance);
    }
}
