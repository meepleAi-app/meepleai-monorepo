using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="ToolkitVersion"/> aggregate
/// (issue #822 — Phase 5 schema foundation).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class ToolkitVersionTests
{
    private static readonly Guid ToolkitId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime PublishedAt = new(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);

    // ========================================================================
    // Publish factory
    // ========================================================================

    [Fact]
    public void Publish_WithValidSemver_CreatesPublishedVersion()
    {
        var version = ToolkitVersion.Publish(
            toolkitId: ToolkitId,
            versionNumber: "1.2.3",
            changelog: "Initial release",
            publishedBy: UserId,
            publishedAt: PublishedAt);

        version.ToolkitId.Should().Be(ToolkitId);
        version.VersionNumber.Should().Be("1.2.3");
        version.Changelog.Should().Be("Initial release");
        version.PublishedBy.Should().Be(UserId);
        version.PublishedAt.Should().Be(PublishedAt);
        version.IsYanked.Should().BeFalse();
        version.YankedAt.Should().BeNull();
        version.YankedBy.Should().BeNull();
        version.YankReason.Should().BeNull();
    }

    [Fact]
    public void Publish_RaisesPublishedDomainEvent()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);

        version.DomainEvents.Should().ContainSingle();
        version.DomainEvents.Should().ContainItemsAssignableTo<ToolkitVersionPublishedEvent>();
        var evt = (ToolkitVersionPublishedEvent)version.DomainEvents.Single();
        evt.ToolkitId.Should().Be(ToolkitId);
        evt.VersionNumber.Should().Be("1.0.0");
        evt.PublishedBy.Should().Be(UserId);
        evt.VersionId.Should().Be(version.Id);
    }

    [Fact]
    public void Publish_NullChangelog_PersistsNull()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);

        version.Changelog.Should().BeNull();
    }

    [Fact]
    public void Publish_WhitespaceChangelog_NormalisedToNull()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", "   ", UserId, PublishedAt);

        version.Changelog.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.2.3-alpha")]
    [InlineData("v1.2.3")]
    [InlineData("abc")]
    public void Publish_InvalidSemver_Throws(string invalidVersion)
    {
        Action act = () => ToolkitVersion.Publish(ToolkitId, invalidVersion, null, UserId, PublishedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*VersionNumber*");
    }

    [Fact]
    public void Publish_EmptyToolkitId_Throws()
    {
        Action act = () => ToolkitVersion.Publish(Guid.Empty, "1.0.0", null, UserId, PublishedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*ToolkitId*");
    }

    [Fact]
    public void Publish_EmptyPublishedBy_Throws()
    {
        Action act = () => ToolkitVersion.Publish(ToolkitId, "1.0.0", null, Guid.Empty, PublishedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*PublishedBy*");
    }

    [Fact]
    public void Publish_ChangelogTooLong_Throws()
    {
        var longChangelog = new string('a', 4001);
        Action act = () => ToolkitVersion.Publish(ToolkitId, "1.0.0", longChangelog, UserId, PublishedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*Changelog*4000*");
    }

    // ========================================================================
    // Yank
    // ========================================================================

    [Fact]
    public void Yank_OnPublishedVersion_MarksYankedAndRaisesEvent()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);
        version.ClearDomainEvents();
        var yankedAt = PublishedAt.AddDays(7);

        version.Yank(yankedBy: UserId, reason: "Critical security regression", yankedAt: yankedAt);

        version.IsYanked.Should().BeTrue();
        version.YankedAt.Should().Be(yankedAt);
        version.YankedBy.Should().Be(UserId);
        version.YankReason.Should().Be("Critical security regression");
        version.DomainEvents.Should().ContainItemsAssignableTo<ToolkitVersionYankedEvent>();
        var evt = (ToolkitVersionYankedEvent)version.DomainEvents.Single();
        evt.Reason.Should().Be("Critical security regression");
        evt.YankedBy.Should().Be(UserId);
        evt.VersionId.Should().Be(version.Id);
    }

    [Fact]
    public void Yank_AlreadyYanked_ThrowsConflict()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);
        version.Yank(UserId, "first yank", PublishedAt.AddHours(1));

        Action act = () => version.Yank(UserId, "second yank", PublishedAt.AddHours(2));

        act.Should().Throw<ConflictException>().WithMessage("*already yanked*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Yank_EmptyReason_Throws(string emptyReason)
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);

        Action act = () => version.Yank(UserId, emptyReason, PublishedAt.AddHours(1));

        act.Should().Throw<ArgumentException>().WithMessage("*reason*");
    }

    [Fact]
    public void Yank_ReasonTooLong_Throws()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);
        var longReason = new string('x', 501);

        Action act = () => version.Yank(UserId, longReason, PublishedAt.AddHours(1));

        act.Should().Throw<ArgumentException>().WithMessage("*500*");
    }

    [Fact]
    public void Yank_EmptyYankedBy_Throws()
    {
        var version = ToolkitVersion.Publish(ToolkitId, "1.0.0", null, UserId, PublishedAt);

        Action act = () => version.Yank(Guid.Empty, "some reason", PublishedAt.AddHours(1));

        act.Should().Throw<ArgumentException>().WithMessage("*YankedBy*");
    }

    // ========================================================================
    // IsStrictlyGreater (monotonicity helper for PR-2 publish command)
    // ========================================================================

    [Theory]
    [InlineData("1.0.0", "0.9.9", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("1.10.0", "1.9.0", true)]     // numeric, not lexicographic
    [InlineData("1.0.0", "1.0.0", false)]      // equal
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("0.9.9", "1.0.0", false)]
    public void IsStrictlyGreater_ComparesNumerically(string candidate, string previous, bool expected)
    {
        ToolkitVersion.IsStrictlyGreater(candidate, previous).Should().Be(expected);
    }

    [Fact]
    public void IsStrictlyGreater_InvalidSemver_Throws()
    {
        Action act = () => ToolkitVersion.IsStrictlyGreater("not-semver", "1.0.0");

        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // BackfillFromLegacy (migration seeding helper)
    // ========================================================================

    [Fact]
    public void BackfillFromLegacy_BypassesDomainEvent()
    {
        var version = ToolkitVersion.BackfillFromLegacy(
            toolkitId: ToolkitId,
            versionNumber: "0.5.0",   // legacy "0.{int}.0" shape from PR #1144
            publishedBy: UserId,
            publishedAt: PublishedAt);

        version.VersionNumber.Should().Be("0.5.0");
        version.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void BackfillFromLegacy_EmptyToolkitId_Throws()
    {
        Action act = () => ToolkitVersion.BackfillFromLegacy(Guid.Empty, "0.1.0", UserId, PublishedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*ToolkitId*");
    }
}

/// <summary>
/// Extension helper to clear domain events on a fresh aggregate so we can
/// assert the events raised by a SPECIFIC operation in isolation. Mirrors
/// the helper used elsewhere in the test suite.
/// </summary>
file static class AggregateExtensions
{
    public static void ClearDomainEvents(this ToolkitVersion version)
    {
        var field = typeof(Api.SharedKernel.Domain.Entities.AggregateRoot<Guid>)
            .GetField("_domainEvents",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var list = (System.Collections.IList?)field?.GetValue(version);
        list?.Clear();
    }
}
