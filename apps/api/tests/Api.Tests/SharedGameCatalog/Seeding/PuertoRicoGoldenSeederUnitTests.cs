using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Seeding;
using Api.Infrastructure.Seeders;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Seeding;

/// <summary>
/// Unit-only tests for <see cref="PuertoRicoGoldenSeeder"/> — extracted from
/// <see cref="PuertoRicoGoldenSeederTests"/> because that class is bound to
/// the Integration-GroupC Testcontainers fixture. xUnit instantiates the
/// collection fixture for ALL tests in the collection, even ones traited
/// <c>Category=Unit</c>, which causes the <c>Backend Fast (build + unit)</c>
/// CI job (no Docker) to fail with <c>DockerUnavailableException</c>.
///
/// These tests rely only on static method calls + an in-memory
/// <see cref="ServiceCollection"/>; no DbContext, no Docker.
///
/// Issue #1873 review follow-up.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PuertoRicoGoldenSeederUnitTests
{
    // ============================================================
    // Embedded resource sanity (pure static calls)
    // ============================================================

    [Fact]
    public void LoadEmbeddedFixture_ParsesAtLeastFiftyClaims()
    {
        var claims = PuertoRicoGoldenSeeder.LoadEmbeddedClaimsForTest();

        claims.Should().NotBeNull();
        claims.Count.Should().BeGreaterThanOrEqualTo(
            50,
            "Sprint 2 spec requires at least 50 curated Puerto Rico claims (Task 1)");

        claims.Should().AllSatisfy(c =>
        {
            c.Statement.Should().NotBeNullOrWhiteSpace();
            c.SourceQuote.Should().NotBeNullOrWhiteSpace();
            c.ExpectedPage.Should().BeGreaterThanOrEqualTo(1);
            // Section is an int 0..5; the enum has 6 members.
            c.Section.Should().BeInRange(0, 5);
        });
    }

    [Fact]
    public void LoadEmbeddedFixture_AllSixSectionsRepresented()
    {
        var claims = PuertoRicoGoldenSeeder.LoadEmbeddedClaimsForTest();

        var sections = claims.Select(c => c.Section).Distinct().OrderBy(s => s).ToArray();
        sections.Should().BeEquivalentTo(
            new[] { 0, 1, 2, 3, 4, 5 },
            "Task 1 fixture audit guarantees ≥1 claim per MechanicSection");
    }

    [Fact]
    public void LoadEmbeddedBggTagsForTest_ReturnsParsedFixture()
    {
        var tags = PuertoRicoGoldenSeeder.LoadEmbeddedBggTagsForTest();

        tags.Should().NotBeNull();
        tags.Count.Should().BeGreaterThanOrEqualTo(
            10,
            "Sprint 2 Task 4 fixture must contain at least 10 BGG tags for Puerto Rico");

        tags.Should().AllSatisfy(t =>
        {
            t.Name.Should().NotBeNullOrWhiteSpace();
            t.Category.Should().NotBeNullOrWhiteSpace();
        });
    }

    // ============================================================
    // Skip flag — never touches DbContext
    // ============================================================

    [Fact]
    public async Task SeedAsync_WhenSkipFlagTrue_ReturnsImmediatelyAndSendsNothing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SKIP_PUERTO_RICO_GOLDEN_SEED"] = "true"
            })
            .Build();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton<IMediator>(mediator.Object)
            .BuildServiceProvider();

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: null!, // Skip path must NOT dereference the DbContext.
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: Guid.NewGuid());

        var seeder = new PuertoRicoGoldenSeeder();

        await seeder.SeedAsync(context, default);

        mediator.VerifyNoOtherCalls();
    }
}
