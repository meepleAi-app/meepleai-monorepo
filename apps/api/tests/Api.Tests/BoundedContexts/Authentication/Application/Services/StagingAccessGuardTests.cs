using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// Unit tests for the DB-backed <see cref="StagingAccessGuard"/> (#845).
/// </summary>
/// <remarks>
/// Replaces the env-var-driven implementation; the test suite is intentionally
/// rebuilt from scratch to exercise the new contract (async, IMemoryCache,
/// fail-closed on empty, scope factory + repository for reads).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class StagingAccessGuardTests
{
    private static StagingAccessGuard CreateGuard(
        IReadOnlySet<string> allowedEmails,
        out IMemoryCache cache,
        out Mock<IStagingAllowlistRepository> repository)
    {
        cache = new MemoryCache(new MemoryCacheOptions());

        repository = new Mock<IStagingAllowlistRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.GetAllowedEmailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedEmails);

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new StagingAccessGuard(cache, scopeFactory);
    }

    [Fact]
    public async Task IsEmailAllowedAsync_WhenAllowlistEmpty_ReturnsFalse_FailClosed()
    {
        // Critical semantic change in #845: empty allowlist now DENIES (was: allows).
        var guard = CreateGuard(new HashSet<string>(), out _, out _);

        (await guard.IsEmailAllowedAsync("anyone@example.com")).Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_WhenEmailInList_ReturnsTrue()
    {
        var allowed = new HashSet<string> { "badsworm@gmail.com", "marco@example.com" };
        var guard = CreateGuard(allowed, out _, out _);

        (await guard.IsEmailAllowedAsync("badsworm@gmail.com")).Should().BeTrue();
        (await guard.IsEmailAllowedAsync("marco@example.com")).Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_WhenEmailNotInList_ReturnsFalse()
    {
        var allowed = new HashSet<string> { "badsworm@gmail.com" };
        var guard = CreateGuard(allowed, out _, out _);

        (await guard.IsEmailAllowedAsync("hacker@evil.com")).Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_NormalizesCase_AndWhitespace()
    {
        // Repository returns the canonical normalized form.
        var allowed = new HashSet<string> { "badsworm@gmail.com" };
        var guard = CreateGuard(allowed, out _, out _);

        (await guard.IsEmailAllowedAsync("BADSWORM@GMAIL.COM")).Should().BeTrue();
        (await guard.IsEmailAllowedAsync("  badsworm@gmail.com  ")).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsEmailAllowedAsync_WhenEmailMissing_ReturnsFalse(string? email)
    {
        var allowed = new HashSet<string> { "badsworm@gmail.com" };
        var guard = CreateGuard(allowed, out _, out _);

        (await guard.IsEmailAllowedAsync(email!)).Should().BeFalse();
    }

    [Fact]
    public async Task HasNonEmptyAllowlistAsync_TracksUnderlyingRepository()
    {
        var guardEmpty = CreateGuard(new HashSet<string>(), out _, out _);
        var guardFull = CreateGuard(new HashSet<string> { "x@y.z" }, out _, out _);

        (await guardEmpty.HasNonEmptyAllowlistAsync()).Should().BeFalse();
        (await guardFull.HasNonEmptyAllowlistAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_CachesResult_HitsRepositoryOnceUntilInvalidate()
    {
        var allowed = new HashSet<string> { "badsworm@gmail.com" };
        var guard = CreateGuard(allowed, out _, out var repo);

        // 3 calls within TTL window
        await guard.IsEmailAllowedAsync("badsworm@gmail.com");
        await guard.IsEmailAllowedAsync("badsworm@gmail.com");
        await guard.IsEmailAllowedAsync("other@example.com");

        repo.Verify(r => r.GetAllowedEmailsAsync(It.IsAny<CancellationToken>()), Times.Once);

        // After invalidation, the next read must hit the repository again
        guard.InvalidateCache();
        await guard.IsEmailAllowedAsync("badsworm@gmail.com");

        repo.Verify(r => r.GetAllowedEmailsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void InvalidateCache_RemovesEntry()
    {
        var guard = CreateGuard(new HashSet<string> { "x@y.z" }, out var cache, out _);
        cache.Set(StagingAccessGuard.CacheKey, new HashSet<string> { "stale@x.y" });

        guard.InvalidateCache();

        cache.TryGetValue(StagingAccessGuard.CacheKey, out object? _).Should().BeFalse();
    }
}
