using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1903 M7.2 — unit tests for <see cref="CatalogSeedFeatureFlag"/>.
/// Verifies fail-closed semantics: any DB / deserialization failure must
/// return <c>false</c> (catalog seed touches third-party APIs and a silent
/// flip-to-on is unsafe).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedFeatureFlagTests
{
    private readonly Mock<IConfigurationService> _configService = new();

    private CatalogSeedFeatureFlag CreateSut() =>
        new(_configService.Object, NullLogger<CatalogSeedFeatureFlag>.Instance);

    [Fact]
    public async Task IsEnabledAsync_KeyAbsent_ReturnsFalse()
    {
        _configService
            .Setup(s => s.GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, null, null))
            .ReturnsAsync((bool?)null);

        var result = await CreateSut().IsEnabledAsync();

        result.Should().BeFalse("default is fail-closed");
    }

    [Fact]
    public async Task IsEnabledAsync_KeySetTrue_ReturnsTrue()
    {
        _configService
            .Setup(s => s.GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, null, null))
            .ReturnsAsync((bool?)true);

        var result = await CreateSut().IsEnabledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_KeySetFalse_ReturnsFalse()
    {
        _configService
            .Setup(s => s.GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, null, null))
            .ReturnsAsync((bool?)false);

        var result = await CreateSut().IsEnabledAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ConfigServiceThrows_FailsClosed()
    {
        _configService
            .Setup(s => s.GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, null, null))
            .ThrowsAsync(new InvalidOperationException("DB unreachable"));

        var result = await CreateSut().IsEnabledAsync();

        result.Should().BeFalse("fail-closed on infrastructure errors");
    }

    [Fact]
    public async Task IsEnabledAsync_CancellationRequested_Propagates()
    {
        _configService
            .Setup(s => s.GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, null, null))
            .ReturnsAsync((bool?)true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await CreateSut().IsEnabledAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void FlagKey_IsStableContract()
    {
        // Flag key is part of the operational contract (admin UI / DB seeds
        // reference the same literal). Lock it down so a careless rename
        // doesn't silently park production deployments.
        ICatalogSeedFeatureFlag.FlagKey.Should().Be("admin.catalog-seed.enabled");
    }
}
