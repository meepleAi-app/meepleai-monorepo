using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Tests for ApproveGameProposalCommandHandler's handling of ProposalApprovalAction.UpdateCover
/// (Task 5: Game Cover-da-PDF — approval promotes the pending cover to L4 on the target SharedGame).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveCoverChangeTests
{
    [Fact]
    public async Task Approve_UpdateCover_SetsL4KeyOnSharedGameAndApproves()
    {
        var target = Guid.NewGuid();
        var sharedGame = SharedGameTestFactory.Existing(target);
        var req = ShareRequest.CreateCoverChange(Guid.NewGuid(), target, Guid.NewGuid(), "covers/g/pdf-cover", 3, null);
        var (handler, adminId) = ApproveHandlerBuilder.For(req, sharedGame);

        var cmd = new ApproveGameProposalCommand(ShareRequestId: req.Id, AdminId: adminId,
            ApprovalAction: ProposalApprovalAction.UpdateCover, TargetSharedGameId: target, AdminNotes: null);

        await handler.Handle(cmd, TestContext.Current.CancellationToken);

        sharedGame.PdfCoverR2Key.Should().Be("covers/g/pdf-cover");
        req.Status.Should().Be(ShareRequestStatus.Approved);
    }

    [Fact]
    public async Task Approve_UpdateCover_WithoutPendingCoverKey_ThrowsConflictException()
    {
        var target = Guid.NewGuid();
        var sharedGame = SharedGameTestFactory.Existing(target);

        // AdditionalContent targeting the same shared game never populates PendingCoverR2Key,
        // exercising the "missing pending cover key" guard in ApproveCoverChangeAsync.
        var req = ShareRequest.Create(Guid.NewGuid(), target, ContributionType.AdditionalContent, null, target);
        var (handler, adminId) = ApproveHandlerBuilder.For(req, sharedGame);

        var cmd = new ApproveGameProposalCommand(ShareRequestId: req.Id, AdminId: adminId,
            ApprovalAction: ProposalApprovalAction.UpdateCover, TargetSharedGameId: target, AdminNotes: null);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Approve_UpdateCover_NonExistentSharedGame_ThrowsNotFoundException()
    {
        var target = Guid.NewGuid();
        var req = ShareRequest.CreateCoverChange(Guid.NewGuid(), target, Guid.NewGuid(), "covers/g/pdf-cover", 3, null);
        var (handler, adminId) = ApproveHandlerBuilder.For(req, sharedGame: null);

        var cmd = new ApproveGameProposalCommand(ShareRequestId: req.Id, AdminId: adminId,
            ApprovalAction: ProposalApprovalAction.UpdateCover, TargetSharedGameId: target, AdminNotes: null);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

/// <summary>
/// Minimal local factory for constructing a SharedGame with a caller-chosen Id, using the
/// internal reconstitution constructor (visible to Api.Tests via InternalsVisibleTo) so tests
/// can align the SharedGame.Id with a ShareRequest's TargetSharedGameId.
/// </summary>
internal static class SharedGameTestFactory
{
    public static SharedGame Existing(Guid id)
    {
        return new SharedGame(
            id: id,
            title: "Test Shared Game",
            yearPublished: 2020,
            description: "A test game",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: null,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow,
            modifiedAt: null,
            isDeleted: false);
    }
}

/// <summary>
/// Minimal local builder that wires an ApproveGameProposalCommandHandler with mocked
/// repositories/services for CoverChange approval tests. Moves the ShareRequest into
/// InReview (required precondition for ShareRequest.Approve) and returns the admin id
/// that started the review, so the caller's command uses a matching AdminId.
/// </summary>
internal static class ApproveHandlerBuilder
{
    public static (ApproveGameProposalCommandHandler Handler, Guid AdminId) For(
        ShareRequest shareRequest,
        SharedGame? sharedGame)
    {
        var adminId = Guid.NewGuid();
        shareRequest.StartReview(adminId);

        var shareRequestRepoMock = new Mock<IShareRequestRepository>();
        shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var privateGameRepoMock = new Mock<IPrivateGameRepository>();

        var sharedGameRepoMock = new Mock<ISharedGameRepository>();
        sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var documentServiceMock = new Mock<IShareRequestDocumentService>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ApproveGameProposalCommandHandler>>();
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext($"ApproveCoverChangeTest_{Guid.NewGuid()}");

        var handler = new ApproveGameProposalCommandHandler(
            shareRequestRepoMock.Object,
            privateGameRepoMock.Object,
            sharedGameRepoMock.Object,
            documentServiceMock.Object,
            dbContext,
            unitOfWorkMock.Object,
            loggerMock.Object);

        return (handler, adminId);
    }
}
