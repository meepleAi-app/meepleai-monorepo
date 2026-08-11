using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class DeleteGlossaryEntryCommandHandlerTests
{
    private static GamebookCampaignSession Campaign(Guid ownerId)
        => GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "My Campaign");

    private static GamebookGlossaryEntry Entry(Guid campaignId, Guid createdBy)
        => GamebookGlossaryEntry.Create(campaignId, "sentinel", "sentinella", GlossarySource.Manual, createdBy);

    [Fact]
    public async Task Handle_ByOwner_RemovesEntryAndSaves()
    {
        var ownerId = Guid.NewGuid();
        var campaign = Campaign(ownerId);
        var entry = Entry(campaign.Id, ownerId);

        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);
        var glossary = new Mock<IGamebookGlossaryRepository>();
        glossary.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        var handler = new DeleteGlossaryEntryCommandHandler(campaigns.Object, glossary.Object);

        await handler.Handle(
            new DeleteGlossaryEntryCommand(campaign.Id, entry.Id, ownerId),
            TestContext.Current.CancellationToken);

        glossary.Verify(r => r.Remove(entry), Times.Once);
        glossary.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ByNonOwner_ThrowsForbiddenAndDoesNotRemove()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var campaign = Campaign(ownerId);

        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);
        var glossary = new Mock<IGamebookGlossaryRepository>();
        var handler = new DeleteGlossaryEntryCommandHandler(campaigns.Object, glossary.Object);

        var act = () => handler.Handle(
            new DeleteGlossaryEntryCommand(campaign.Id, Guid.NewGuid(), otherUserId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        glossary.Verify(r => r.Remove(It.IsAny<GamebookGlossaryEntry>()), Times.Never);
        glossary.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CampaignNotFound_ThrowsNotFound()
    {
        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookCampaignSession?)null);
        var glossary = new Mock<IGamebookGlossaryRepository>();
        var handler = new DeleteGlossaryEntryCommandHandler(campaigns.Object, glossary.Object);

        var act = () => handler.Handle(
            new DeleteGlossaryEntryCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_EntryNotFound_ThrowsNotFoundAndDoesNotRemove()
    {
        var ownerId = Guid.NewGuid();
        var campaign = Campaign(ownerId);

        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);
        var glossary = new Mock<IGamebookGlossaryRepository>();
        glossary.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);
        var handler = new DeleteGlossaryEntryCommandHandler(campaigns.Object, glossary.Object);

        var act = () => handler.Handle(
            new DeleteGlossaryEntryCommand(campaign.Id, Guid.NewGuid(), ownerId),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
        glossary.Verify(r => r.Remove(It.IsAny<GamebookGlossaryEntry>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EntryFromAnotherCampaign_ThrowsNotFoundAndDoesNotRemove()
    {
        var ownerId = Guid.NewGuid();
        var campaign = Campaign(ownerId);
        var foreignEntry = Entry(Guid.NewGuid(), ownerId); // belongs to a different campaign

        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);
        var glossary = new Mock<IGamebookGlossaryRepository>();
        glossary.Setup(r => r.GetByIdAsync(foreignEntry.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreignEntry);
        var handler = new DeleteGlossaryEntryCommandHandler(campaigns.Object, glossary.Object);

        var act = () => handler.Handle(
            new DeleteGlossaryEntryCommand(campaign.Id, foreignEntry.Id, ownerId),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
        glossary.Verify(r => r.Remove(It.IsAny<GamebookGlossaryEntry>()), Times.Never);
    }
}
