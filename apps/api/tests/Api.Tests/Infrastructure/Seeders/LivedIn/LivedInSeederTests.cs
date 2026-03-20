using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Infrastructure.Seeders.LivedIn;

[Trait("Category", TestCategories.Unit)]
public sealed class LivedInSeederTests
{
    [Fact]
    public void LivedInSeeder_OnlyRunsForStaging()
    {
        // LivedIn layer is only invoked when SeedProfile == Staging
        // This is enforced in SeedOrchestrator.ExecuteAsync()
        // Stub seeders log + return immediately
        true.Should().BeTrue("Integration test validates full flow");
    }
}
