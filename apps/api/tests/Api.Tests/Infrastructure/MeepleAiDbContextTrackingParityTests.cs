using Api.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.SharedKernel.Application.Services;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3866: production configures <c>QueryTrackingBehavior.NoTracking</c> on the DbContext
/// options (PERF-06, <c>InfrastructureServiceExtensions.cs:180</c>). An entity returned by a query
/// is therefore NOT tracked: mutating it and calling <c>SaveChangesAsync</c> writes nothing, and
/// raises nothing either.
///
/// Test fixtures build their own <c>MeepleAiDbContext</c> and, without that option, get EF Core's
/// tracking-by-default. In that configuration the whole family of "the handler mutated an untracked
/// entity" defects is invisible — it has shipped five times (#1627, #1633, #2804, #3564, #3858),
/// found in production or by an audit, never by a test. On #3858 the first regression test passed
/// against the broken code, and only turned red once the test context reproduced NoTracking.
///
/// The parity is enforced in <c>OnConfiguring</c> rather than left to each fixture: 287 test files
/// build a context, and a rule that has to be remembered 287 times is not a rule. It deliberately
/// does NOT live in the constructor — see <see cref="ConstructingContext_DoesNotForceTheModelToBeBuilt"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Issue", "3866")]
public sealed class MeepleAiDbContextTrackingParityTests
{
    private static MeepleAiDbContext CreateContext(
        Action<DbContextOptionsBuilder<MeepleAiDbContext>>? configure = null)
    {
        var builder = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"TrackingParity_{Guid.NewGuid()}");
        configure?.Invoke(builder);

        return new MeepleAiDbContext(
            builder.Options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    /// <summary>
    /// A context built the way fixtures build it — no tracking option at all — must behave like
    /// production. This is the assertion that was missing: without it, a fixture silently gets
    /// tracking-by-default and covers up the defect it was written to catch.
    /// </summary>
    [Fact]
    public void ContextBuiltWithoutTrackingOption_DefaultsToNoTracking_LikeProduction()
    {
        using var db = CreateContext();

        db.ChangeTracker.QueryTrackingBehavior.Should().Be(
            QueryTrackingBehavior.NoTracking,
            "production sets NoTracking on the options (PERF-06); a test context that tracks makes a whole family of defects invisible");
    }

    /// <summary>
    /// The parity must not change WHEN the model is built. Assigning
    /// <c>ChangeTracker.QueryTrackingBehavior</c> in the constructor forces the ChangeTracker — and
    /// with it the model — to be built at construction time, turning every latent model
    /// misconfiguration into a failure at <c>new MeepleAiDbContext(...)</c> even for a context nobody
    /// queries. SQLite cannot map pgvector's <c>Vector</c>, so a fixture that swaps in SQLite to test
    /// routing only (<c>EndpointContractTests</c>) died on "No suitable constructor was found for
    /// entity type 'Vector'". <c>OnConfiguring</c> runs before the ChangeTracker exists, so the
    /// default applies and the model stays lazy.
    /// </summary>
    [Fact]
    public void ConstructingContext_DoesNotForceTheModelToBeBuilt()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var act = () =>
        {
            using var db = new MeepleAiDbContext(
                options,
                new Mock<IMediator>().Object,
                new Mock<IDomainEventCollector>().Object);
        };

        act.Should().NotThrow(
            "a context that is never queried must not pay for — nor fail on — building the model");
    }

    /// <summary>
    /// An opt-out has to exist: a fixture whose subject genuinely needs tracking must be able to say
    /// so, or the parity becomes a straitjacket. It is expressed ON THE INSTANCE, not on the options —
    /// <c>CoreOptionsExtension.QueryTrackingBehavior</c> already reads <c>TrackAll</c> when nothing was
    /// configured, so <c>OnConfiguring</c> cannot tell an explicit choice from an absent one and
    /// defaults unconditionally. This test pins the seam that a fixture is expected to use.
    /// </summary>
    [Fact]
    public void FixtureThatNeedsTracking_CanOptOutOnTheInstance()
    {
        using var db = CreateContext();
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        db.ChangeTracker.QueryTrackingBehavior.Should().Be(
            QueryTrackingBehavior.TrackAll,
            "a fixture must stay able to opt out, explicitly and visibly, right where it is built");
    }
}
