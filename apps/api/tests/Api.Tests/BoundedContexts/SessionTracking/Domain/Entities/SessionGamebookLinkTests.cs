using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// #2632 (SI-1): the optional Session -> GamebookCampaignSession link (D-LINK).
/// A GameNight-attached gamebook sitting IS a Session that points at the persistent campaign.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionGamebookLinkTests
{
    [Fact]
    public void Create_WithGamebookCampaignId_SetsTheLink()
    {
        var campaignId = Guid.NewGuid();

        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.Generic,
            gamebookCampaignId: campaignId);

        session.GamebookCampaignId.Should().Be(campaignId);
    }

    [Fact]
    public void Create_WithoutGamebookCampaignId_LeavesLinkNull()
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.Generic);

        session.GamebookCampaignId.Should().BeNull();
    }

    [Fact]
    public void Create_WithGamebookLink_StillSeedsOwnerParticipant()
    {
        // The link must not disturb the existing owner-participant seeding.
        var userId = Guid.NewGuid();

        var session = Session.Create(
            userId: userId,
            gameId: Guid.NewGuid(),
            sessionType: SessionType.Generic,
            gamebookCampaignId: Guid.NewGuid());

        session.Participants.Should().ContainSingle(p => p.IsOwner && p.UserId == userId);
    }
}
