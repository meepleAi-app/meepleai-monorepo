using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// #5079: the owner withdraws the open proposal of a private game. With a proposal open the game cannot lose its
/// description nor be deleted (decision of 2026-11-19); withdrawing frees it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class WithdrawPrivateGameProposalCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepositoryMock = new();
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly WithdrawPrivateGameProposalCommandHandler _handler;

    private static readonly Guid OwnerId = Guid.NewGuid();

    public WithdrawPrivateGameProposalCommandHandlerTests()
    {
        _handler = new WithdrawPrivateGameProposalCommandHandler(
            _privateGameRepositoryMock.Object,
            _shareRequestRepositoryMock.Object,
            _unitOfWorkMock.Object,
            Mock.Of<ILogger<WithdrawPrivateGameProposalCommandHandler>>());
    }

    [Fact]
    public async Task Handle_PendingProposal_IsWithdrawnAndSaved()
    {
        var game = Seed(OwnerId);
        var proposal = Open(game, ShareRequestStatus.Pending);
        ShareRequest? updated = null;
        _shareRequestRepositoryMock.Setup(r => r.Update(It.IsAny<ShareRequest>())).Callback<ShareRequest>(r => updated = r);

        await _handler.Handle(new WithdrawPrivateGameProposalCommand(OwnerId, game.Id), TestContext.Current.CancellationToken);

        updated.Should().BeSameAs(proposal);
        proposal.Status.Should().Be(ShareRequestStatus.Withdrawn);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProposalInReview_ThrowsConflictInReview_AndSavesNothing()
    {
        // The domain does not withdraw a request an admin holds in review; the user is told to wait for the decision.
        var game = Seed(OwnerId);
        var proposal = Open(game, ShareRequestStatus.InReview);

        var act = () => _handler.Handle(
            new WithdrawPrivateGameProposalCommand(OwnerId, game.Id), TestContext.Current.CancellationToken);

        var conflict = await act.Should().ThrowAsync<ConflictException>();
        conflict.Which.ErrorCode.Should().Be(PrivateGameProposalCodes.InReview);
        proposal.Status.Should().Be(ShareRequestStatus.InReview);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoOpenProposal_ThrowsNotFound()
    {
        var game = Seed(OwnerId);

        var act = () => _handler.Handle(
            new WithdrawPrivateGameProposalCommand(OwnerId, game.Id), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SomeoneElsesGame_ThrowsForbidden_AndWithdrawsNothing()
    {
        var game = Seed(Guid.NewGuid());
        var proposal = Open(game, ShareRequestStatus.Pending);

        var act = () => _handler.Handle(
            new WithdrawPrivateGameProposalCommand(OwnerId, game.Id), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        proposal.Status.Should().Be(ShareRequestStatus.Pending);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownGame_ThrowsNotFound()
    {
        var act = () => _handler.Handle(
            new WithdrawPrivateGameProposalCommand(OwnerId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private PrivateGame Seed(Guid ownerId)
    {
        var game = PrivateGame.CreateManual(ownerId, "Hand-made", 2, 4, description: "A tile game.");
        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        return game;
    }

    private ShareRequest Open(PrivateGame game, ShareRequestStatus status)
    {
        var proposal = ShareRequest.CreateGameProposal(game.OwnerId, game.Id);
        if (status == ShareRequestStatus.InReview) proposal.StartReview(Guid.NewGuid());
        _shareRequestRepositoryMock
            .Setup(r => r.GetPendingProposalForPrivateGameAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);
        return proposal;
    }
}
