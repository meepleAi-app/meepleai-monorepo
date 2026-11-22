using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// #5078: the game a share request's notifications name. A proposal's SourceGameId is Guid.Empty, so looking up a
/// shared game with it skipped every notification of a proposal.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class ShareRequestNotificationGameTests
{
    private readonly Mock<ISharedGameRepository> _sharedGames = new();
    private readonly Mock<IPrivateGameRepository> _privateGames = new();

    [Fact]
    public async Task AProposal_IsNamedAfterItsPrivateGame_WithoutItsImage()
    {
        var ownerId = Guid.NewGuid();
        var privateGame = PrivateGame.CreateManual(ownerId, "Il mio gioco", 2, 4, imageUrl: "https://example.com/box.jpg");
        var proposal = ShareRequest.CreateGameProposal(ownerId, privateGame.Id);
        _privateGames.Setup(r => r.GetByIdAsync(privateGame.Id, It.IsAny<CancellationToken>())).ReturnsAsync(privateGame);

        var game = await FindAsync(proposal);

        game.Should().Be(new ShareRequestNotificationGame("Il mio gioco", ImageUrl: null));
        _sharedGames.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AProposalWhosePrivateGameIsGone_HasNoGame()
    {
        var proposal = ShareRequest.CreateGameProposal(Guid.NewGuid(), Guid.NewGuid());

        var game = await FindAsync(proposal);

        game.Should().BeNull();
    }

    [Fact]
    public async Task AnyOtherRequest_IsNamedAfterItsSharedGame_WithItsImage()
    {
        var sharedGame = SharedGame.Create("Gioco del catalogo", 2020, "Descrizione", 1, 4, 60, 10, 2.5m, 7.5m,
            "https://example.com/cover.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);
        var request = ShareRequest.Create(Guid.NewGuid(), sharedGame.Id, ContributionType.NewGame);
        _sharedGames.Setup(r => r.GetByIdAsync(sharedGame.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sharedGame);

        var game = await FindAsync(request);

        game.Should().Be(new ShareRequestNotificationGame("Gioco del catalogo", "https://example.com/cover.jpg"));
        _privateGames.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARequestWhoseSharedGameIsGone_HasNoGame()
    {
        var request = ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame);

        var game = await FindAsync(request);

        game.Should().BeNull();
    }

    private Task<ShareRequestNotificationGame?> FindAsync(ShareRequest request) =>
        ShareRequestNotificationGame.FindAsync(
            request, _sharedGames.Object, _privateGames.Object, TestContext.Current.CancellationToken);
}
