using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Tests for <see cref="ProposePrivateGameCommandHandler"/>: the proposal of a private game to the shared catalog,
/// restored by #5074 with the description as a requirement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class ProposePrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepositoryMock = new();
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock = new();
    private readonly Mock<IRateLimitEvaluator> _rateLimitEvaluatorMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ProposePrivateGameCommandHandler _handler;

    private static readonly Guid OwnerId = Guid.NewGuid();

    public ProposePrivateGameCommandHandlerTests()
    {
        // Under the share-request limits unless a test says otherwise (#5079).
        _rateLimitEvaluatorMock
            .Setup(r => r.CanUserCreateRequestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new ProposePrivateGameCommandHandler(
            _privateGameRepositoryMock.Object,
            _shareRequestRepositoryMock.Object,
            _rateLimitEvaluatorMock.Object,
            _unitOfWorkMock.Object,
            Mock.Of<ILogger<ProposePrivateGameCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ManualGameWithDescription_CreatesAProposalAndSavesIt()
    {
        // A hand-made game can be proposed too: the description decides, not a BGG origin (#5074).
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        ShareRequest? added = null;
        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ShareRequest, CancellationToken>((request, _) => added = request);

        var response = await _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id, "Please add it"),
            TestContext.Current.CancellationToken);

        added.Should().NotBeNull();
        added!.ContributionType.Should().Be(ContributionType.NewGameProposal);
        added.SourcePrivateGameId.Should().Be(game.Id);
        added.SourceGameId.Should().Be(Guid.Empty);
        added.UserNotes.Should().Be("Please add it");
        response.ShareRequestId.Should().Be(added.Id);
        response.ContributionType.Should().Be(ContributionType.NewGameProposal);
        response.Status.Should().Be(ShareRequestStatus.Pending);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_GameWithoutDescription_ThrowsValidationOnDescription_AndSavesNothing(string? description)
    {
        // #5074, decision of 2026-11-19: without a description the proposal does not start. It used to fail
        // later, at the admin's approval, with a 400.
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: description));

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        var error = await act.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Description");
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_UnknownGame_ThrowsNotFound()
    {
        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_SomeoneElsesGame_ThrowsForbidden()
    {
        var game = Seed(PrivateGame.CreateManual(Guid.NewGuid(), "Not mine", 2, 4, description: "A tile game."));

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_WithAPendingProposal_ThrowsConflict()
    {
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        _shareRequestRepositoryMock
            .Setup(r => r.GetPendingProposalForPrivateGameAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareRequest.CreateGameProposal(OwnerId, game.Id));

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        var conflict = await act.Should().ThrowAsync<ConflictException>();
        conflict.Which.ErrorCode.Should().Be(PrivateGameProposalCodes.AlreadyOpen);
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_GameWhoseProposalWasApproved_ThrowsConflictAlreadyApproved()
    {
        // #5079, decision of 2026-11-19: once approved the game is in the catalog, and is not proposed again.
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        _shareRequestRepositoryMock
            .Setup(r => r.HasApprovedProposalForPrivateGameAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        var conflict = await act.Should().ThrowAsync<ConflictException>();
        conflict.Which.ErrorCode.Should().Be(PrivateGameProposalCodes.AlreadyApproved);
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_UserAtTheShareRequestLimits_ThrowsTooManyRequests()
    {
        // #5079, decision of 2026-11-19: a proposal is a share request and takes the same limits; it used to skip them
        // while the counters counted it.
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        _rateLimitEvaluatorMock
            .Setup(r => r.CanUserCreateRequestAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        var refused = await act.Should().ThrowAsync<TooManyRequestsException>();
        refused.Which.ErrorCode.Should().Be(PrivateGameProposalCodes.LimitReached);
        VerifyNothingStored();
    }

    [Fact]
    public async Task Handle_ConcurrentProposalStoredFirst_MapsTheUniqueIndexToConflict()
    {
        // #5079: two concurrent requests both pass the pending check; the database index refuses the second insert.
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolation(ProposePrivateGameCommandHandler.OneProposalIndex));

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        var conflict = await act.Should().ThrowAsync<ConflictException>();
        conflict.Which.ErrorCode.Should().Be(PrivateGameProposalCodes.AlreadyOpen);
    }

    [Fact]
    public async Task Handle_AnotherUniqueViolation_IsNotTakenForTheProposalOne()
    {
        var game = Seed(PrivateGame.CreateManual(OwnerId, "Hand-made", 2, 4, description: "A tile game."));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolation("pk_share_requests"));

        var act = () => _handler.Handle(
            new ProposePrivateGameCommand(OwnerId, game.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    private static Microsoft.EntityFrameworkCore.DbUpdateException UniqueViolation(string constraint) =>
        new("unique violation", new Npgsql.PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR", "23505", constraintName: constraint));

    private PrivateGame Seed(PrivateGame game)
    {
        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        return game;
    }

    private void VerifyNothingStored()
    {
        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
