using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="StorageLayoutOptions"/>.
///
/// Issue #1314 PR 2 introduced WriteMode/ReadMode/LayoutVersionLabel for the
/// 5-phase rollout. Issue #1399 (Phase 4 cleanup) removed those properties
/// after the staging migration converged on the "v2-categorized" layout.
/// Only the <see cref="StorageLayoutOptions.MigrationEnabled"/> toggle remains.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1399")]
public sealed class StorageLayoutOptionsTests
{
    [Fact]
    public void Defaults_MigrationEnabled_IsFalse()
    {
        // The drainer is opt-in by default so a fresh deploy does not start
        // moving blobs without an explicit operator flip.
        var options = new StorageLayoutOptions();

        options.MigrationEnabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigBinding_MigrationEnabledTrue_WhenStringTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageLayout:MigrationEnabled"] = "true",
            })
            .Build();

        var bound = config.GetSection(StorageLayoutOptions.SectionName).Get<StorageLayoutOptions>();
        bound.Should().NotBeNull();
        bound!.MigrationEnabled.Should().BeTrue();
    }

    [Fact]
    public void ConfigBinding_MigrationEnabledFalse_WhenStringFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageLayout:MigrationEnabled"] = "false",
            })
            .Build();

        var bound = config.GetSection(StorageLayoutOptions.SectionName).Get<StorageLayoutOptions>();
        bound.Should().NotBeNull();
        bound!.MigrationEnabled.Should().BeFalse();
    }
}
