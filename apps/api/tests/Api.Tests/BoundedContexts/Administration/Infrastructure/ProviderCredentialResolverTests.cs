using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Unit tests for <see cref="ProviderCredentialResolver"/>. Issue #1859 — Task 7.
/// Covers DB-active hit, configuration fallback, throw-when-not-configured, cache-hit-no-repo, and
/// cache invalidation semantics.
///
/// Uses <see cref="EphemeralDataProtectionProvider"/> (in-memory key ring) so the encrypt/decrypt
/// round-trip is real — this matches what the production wiring does and avoids fragile mocks of
/// the byte-level <see cref="IDataProtector"/> contract.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderCredentialResolverTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// #3887: the API-key fallback is read from <see cref="IConfiguration"/>, not from the process
    /// environment, so these tests declare the value instead of mutating a global.
    /// </summary>
    private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => e.Value))
            .Build();

    [Fact]
    public async Task ResolveAsync_DbActiveExists_ReturnsDecryptedPlaintext()
    {
        // Arrange
        const string plaintext = "sk-decrypted-key";
        var protectionProvider = new EphemeralDataProtectionProvider();
        var protector = protectionProvider.CreateProtector(ProviderCredentialResolver.DataProtectionPurpose);
        var ciphertext = protector.Protect(plaintext);
        var credential = CreateActiveCredential("deepseek", ciphertext);

        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(credential));

        var sut = new ProviderCredentialResolver(
            repo,
            protectionProvider,
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Act
        var result = await sut.ResolveAsync("deepseek", CancellationToken.None);

        // Assert
        result.Should().Be(plaintext);
        await repo.Received(1).GetActiveAsync("deepseek", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_DbActiveExists_NormalizesCasing_LooksUpLowercase()
    {
        var protectionProvider = new EphemeralDataProtectionProvider();
        var ciphertext = protectionProvider
            .CreateProtector(ProviderCredentialResolver.DataProtectionPurpose)
            .Protect("plain");
        var credential = CreateActiveCredential("deepseek", ciphertext);

        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(credential));

        var sut = new ProviderCredentialResolver(
            repo,
            protectionProvider,
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        await sut.ResolveAsync("DeepSeek", CancellationToken.None);

        // Looked up by normalized lowercase name in repository
        await repo.Received(1).GetActiveAsync("deepseek", Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetActiveAsync("DeepSeek", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NoDbButEnvVar_ReturnsEnvValue()
    {
        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        var protectionProvider = Substitute.For<IDataProtectionProvider>();

        var sut = new ProviderCredentialResolver(
            repo,
            protectionProvider,
            new MemoryCache(new MemoryCacheOptions()),
            Config(("DEEPSEEK_API_KEY", "sk-from-env")),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Act
        var result = await sut.ResolveAsync("deepseek", CancellationToken.None);

        // Assert
        result.Should().Be("sk-from-env");
        // No decryption attempted in the configuration-fallback path
        protectionProvider.DidNotReceive().CreateProtector(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveAsync_NeitherConfigured_ThrowsProviderCredentialNotConfigured()
    {
        // Arrange — no configured key + DB returns null
        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(null));

        var sut = new ProviderCredentialResolver(
            repo,
            new EphemeralDataProtectionProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Act / Assert
        var act = async () => await sut.ResolveAsync("deepseek", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ProviderCredentialNotConfiguredException>()).Which;
        ex.ProviderName.Should().Be("deepseek");
    }

    [Fact]
    public async Task ResolveAsync_CacheHit_DoesNotCallRepository()
    {
        // Arrange
        const string plaintext = "plain-1";
        var protectionProvider = new EphemeralDataProtectionProvider();
        var ciphertext = protectionProvider
            .CreateProtector(ProviderCredentialResolver.DataProtectionPurpose)
            .Protect(plaintext);
        var credential = CreateActiveCredential("deepseek", ciphertext);

        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(credential));

        var sut = new ProviderCredentialResolver(
            repo,
            protectionProvider,
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Act — two consecutive calls
        var first = await sut.ResolveAsync("deepseek", CancellationToken.None);
        var second = await sut.ResolveAsync("deepseek", CancellationToken.None);

        // Assert
        first.Should().Be(plaintext);
        second.Should().Be(plaintext);
        await repo.Received(1).GetActiveAsync("deepseek", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_RemovesFromCache_NextCallHitsRepoAgain()
    {
        // Arrange
        const string plaintext = "plain-1";
        var protectionProvider = new EphemeralDataProtectionProvider();
        var ciphertext = protectionProvider
            .CreateProtector(ProviderCredentialResolver.DataProtectionPurpose)
            .Protect(plaintext);
        var credential = CreateActiveCredential("deepseek", ciphertext);

        var repo = Substitute.For<IProviderCredentialRepository>();
        repo.GetActiveAsync("deepseek", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProviderCredential?>(credential));

        var sut = new ProviderCredentialResolver(
            repo,
            protectionProvider,
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Act
        await sut.ResolveAsync("deepseek", CancellationToken.None); // 1st: repo hit
        await sut.ResolveAsync("deepseek", CancellationToken.None); // 2nd: cache hit, no repo call

        await repo.Received(1).GetActiveAsync("deepseek", Arg.Any<CancellationToken>());

        sut.Invalidate("deepseek");
        await sut.ResolveAsync("deepseek", CancellationToken.None); // 3rd: cache empty → repo hit again

        // Assert
        await repo.Received(2).GetActiveAsync("deepseek", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Invalidate_NormalizesCasing()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("provider_cred:deepseek", "secret-value");

        var sut = new ProviderCredentialResolver(
            Substitute.For<IProviderCredentialRepository>(),
            new EphemeralDataProtectionProvider(),
            cache,
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        // Invalidate with mixed-case name still hits the lowercase cache key
        sut.Invalidate("DeepSeek");

        cache.TryGetValue("provider_cred:deepseek", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_EmptyProviderName_Throws(string providerName)
    {
        var sut = new ProviderCredentialResolver(
            Substitute.For<IProviderCredentialRepository>(),
            new EphemeralDataProtectionProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Config(),
            NullLogger<ProviderCredentialResolver>.Instance);

        var act = async () => await sut.ResolveAsync(providerName, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ProviderCredential CreateActiveCredential(string name, string ciphertext)
    {
        return ProviderCredential.Create(
            ProviderName.Create(name),
            ciphertext,
            KeyFingerprint.FromPlaintext("sk-de-abcd1234"),
            Guid.NewGuid(),
            previousCredentialId: null,
            new FakeTimeProvider(FixedNow));
    }
}
