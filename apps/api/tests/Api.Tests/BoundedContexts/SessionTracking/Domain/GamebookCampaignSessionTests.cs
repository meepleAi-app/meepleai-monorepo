using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookCampaignSessionTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static GameRef SharedRef() => GameRef.Shared(GameId);

    [Fact]
    public void Create_WithValidInputs_InitializesEmptyProgress()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "Campagna Nanolith #1");

        session.GameRef.Id.Should().Be(GameId);
        session.GameRef.Kind.Should().Be(GameRefKind.Shared);
        session.OwnerUserId.Should().Be(OwnerId);
        session.Title.Should().Be("Campagna Nanolith #1");
        session.Progress.CurrentParagraph.Should().Be(0);
        session.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyTitle_Throws()
    {
        Action act = () => GamebookCampaignSession.Create(SharedRef(), OwnerId, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProgress_AdvancesParagraphAndStampsUpdatedAt()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        var before = session.UpdatedAt;
        Thread.Sleep(50);

        session.UpdateProgress(currentParagraph: 47);

        session.Progress.CurrentParagraph.Should().Be(47);
        session.Progress.History.Should().Contain(47);
        session.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public void SoftDelete_FlagsAsDeletedWithTimestamp()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        session.SoftDelete(deletedBy: OwnerId);

        session.IsDeleted.Should().BeTrue();
        session.DeletedAt.Should().NotBeNull();
    }

    // ── A0.2 (#1320): GameRef discriminator tests ─────────────────────────────

    [Fact]
    public void Create_WithGameRef_StoresAsDiscriminator()
    {
        var sharedId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(
            gameRef: GameRef.Shared(sharedId),
            ownerUserId: Guid.NewGuid(),
            title: "Campagna 1");

        Assert.Equal(GameRefKind.Shared, session.GameRef.Kind);
        Assert.Equal(sharedId, session.GameRef.Id);
    }

    [Fact]
    public void Create_WithPrivateGameRef_StoresAsDiscriminator()
    {
        var privateId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(
            gameRef: GameRef.Private(privateId),
            ownerUserId: Guid.NewGuid(),
            title: "Campagna privata");

        Assert.Equal(GameRefKind.Private, session.GameRef.Kind);
    }
}
