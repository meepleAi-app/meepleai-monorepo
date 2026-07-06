using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for the summary share-token + archive domain behaviour on
/// <see cref="GameNightEvent"/> — Issue #2702.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Feature", "GameNightSummary")]
public class GameNightEventSummaryShareTests
{
    private static GameNightEvent CompletedNight()
    {
        var night = GameNightEvent.Create(Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddDays(1));
        night.Publish(new List<Guid>());
        night.Complete();
        return night;
    }

    [Fact]
    public void GenerateShareToken_SetsUrlSafeTokenAndSharedFlag()
    {
        var night = CompletedNight();

        var token = night.GenerateShareToken();

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain("/").And.NotContain("+").And.NotEndWith("=");
        night.ShareToken.Should().Be(token);
        night.IsShared.Should().BeTrue();
    }

    [Fact]
    public void GenerateShareToken_IsIdempotentlyRotatable()
    {
        var night = CompletedNight();

        var first = night.GenerateShareToken();
        var second = night.GenerateShareToken();

        second.Should().NotBe(first);
        night.ShareToken.Should().Be(second);
    }

    [Fact]
    public void RevokeShareToken_ClearsTokenAndSharedFlag()
    {
        var night = CompletedNight();
        night.GenerateShareToken();

        night.RevokeShareToken();

        night.ShareToken.Should().BeNull();
        night.IsShared.Should().BeFalse();
    }

    [Fact]
    public void Archive_OnCompletedNight_SetsArchivedFlag()
    {
        var night = CompletedNight();

        night.Archive();

        night.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Archive_OnNonCompletedNight_ThrowsConflict()
    {
        var night = GameNightEvent.Create(Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddDays(1));
        night.Publish(new List<Guid>()); // Published, not Completed

        var act = () => night.Archive();

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Unarchive_ClearsArchivedFlag()
    {
        var night = CompletedNight();
        night.Archive();

        night.Unarchive();

        night.IsArchived.Should().BeFalse();
    }
}
