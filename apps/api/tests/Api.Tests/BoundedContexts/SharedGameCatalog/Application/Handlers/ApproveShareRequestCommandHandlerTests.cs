using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveShareRequestCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _repositoryMock;
    private readonly Mock<IShareRequestDocumentService> _documentsMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ApproveShareRequestCommandHandler>> _loggerMock;
    private readonly ApproveShareRequestCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public ApproveShareRequestCommandHandlerTests()
    {
        _repositoryMock = new Mock<IShareRequestRepository>();
        _documentsMock = new Mock<IShareRequestDocumentService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ApproveShareRequestCommandHandler>>();

        // As the real service when every copy succeeds: one new document id for each source.
        _documentsMock
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, Guid _, Guid _, CancellationToken _) => ids.Select(_ => Guid.NewGuid()).ToList());

        _handler = new ApproveShareRequestCommandHandler(
            _repositoryMock.Object,
            _documentsMock.Object,
            Mock.Of<IContributorRepository>(),
            _unitOfWorkMock.Object,
            Mock.Of<ISharedGameCacheInvalidator>(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ApprovesShareRequest()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            AdminNotes: "Looks good!");

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        ShareRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(req => capturedRequest = req);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.ShareRequestId.Should().Be(shareRequest.Id);
        response.Status.Should().Be(ShareRequestStatus.Approved);
        response.ResolvedAt.Should().NotBe(default);

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest.Status.Should().Be(ShareRequestStatus.Approved);
    }

    [Fact]
    public async Task Handle_WithTheRequestsOwnDestinationAsTarget_SetsTargetGame()
    {
        // Arrange: a NewGame contributes to its source game (#5109), so that is the one target it accepts.
        var targetGameId = TestGameId;
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            TargetSharedGameId: targetGameId);

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        ShareRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(req => capturedRequest = req);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.TargetSharedGameId.Should().Be(targetGameId);
        response.TargetSharedGameId.Should().Be(targetGameId);
    }

    [Fact]
    public async Task Handle_WithNonExistentShareRequest_ThrowsNotFoundException()
    {
        // Arrange
        var command = new ApproveShareRequestCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId);

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWrongAdmin_ThrowsDomainException()
    {
        // Arrange
        var differentAdminId = Guid.NewGuid();
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId); // Wrong admin

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert - Domain throws ShareRequestReviewerMismatchException
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Exception>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // #5074: approving a NewGameProposal here would mark it approved without creating the shared game.
    [Fact]
    public async Task Handle_NewGameProposal_ThrowsConflict_AndChangesNothing()
    {
        // Arrange
        var proposal = ShareRequest.CreateGameProposal(TestUserId, Guid.NewGuid(), "Please add it");
        proposal.StartReview(TestAdminId);
        var command = new ApproveShareRequestCommand(ShareRequestId: proposal.Id, AdminId: TestAdminId);

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(proposal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*approve-game-proposal*");
        proposal.Status.Should().Be(ShareRequestStatus.InReview);
        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // #5076: approving a CoverChange here marked it approved and never promoted its pending cover (UpdateCover does).
    [Fact]
    public async Task Handle_CoverChange_ThrowsConflict_AndChangesNothing()
    {
        // Arrange
        var coverChange = ShareRequest.CreateCoverChange(
            TestUserId, Guid.NewGuid(), Guid.NewGuid(), "covers/g/pdf-cover", 3, null);
        coverChange.StartReview(TestAdminId);
        var command = new ApproveShareRequestCommand(ShareRequestId: coverChange.Id, AdminId: TestAdminId);

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(coverChange.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coverChange);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*CoverChange*approve-game-proposal*");
        coverChange.Status.Should().Be(ShareRequestStatus.InReview);
        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // #5109, decision of 2026-11-23: approval copies the attached documents into the game the request contributes
    // to, once, in the same save as the approval. A NewGame's is its source game, which is already a shared game.
    [Fact]
    public async Task Handle_NewGameWithAttachedDocuments_CopiesThemIntoItsSourceGame_Once()
    {
        var documentId = Guid.NewGuid();
        var request = ShareRequest.Create(TestUserId, TestGameId, ContributionType.NewGame, "With the rulebook");
        request.AttachDocument(documentId, "rules.pdf", "application/pdf", 1024);
        request.StartReview(TestAdminId);
        GivenTheRepositoryReturns(request);

        await _handler.Handle(
            new ApproveShareRequestCommand(ShareRequestId: request.Id, AdminId: TestAdminId),
            TestContext.Current.CancellationToken);

        request.Status.Should().Be(ShareRequestStatus.Approved);
        _documentsMock.Verify(d => d.CopyDocumentsToSharedGame(
            It.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { documentId })),
            TestGameId,
            TestUserId,
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdditionalContentWithAttachedDocuments_CopiesThemIntoTheGameItNamed_Once()
    {
        var existingGameId = Guid.NewGuid();
        var request = ShareRequest.Create(
            TestUserId, TestGameId, ContributionType.AdditionalContent, "An expansion", existingGameId);
        request.AttachDocument(Guid.NewGuid(), "expansion.pdf", "application/pdf", 1024);
        request.StartReview(TestAdminId);
        GivenTheRepositoryReturns(request);

        await _handler.Handle(
            new ApproveShareRequestCommand(ShareRequestId: request.Id, AdminId: TestAdminId),
            TestContext.Current.CancellationToken);

        VerifyCopiesInto(existingGameId, Times.Once());
        VerifyCopiesInto(TestGameId, Times.Never());
    }

    [Fact]
    public async Task Handle_WithATargetNamingAnotherGame_ThrowsConflict_AndCopiesNothing()
    {
        // The destination is the request's, not the HTTP call's: approve-game-proposal refuses a mismatch the same way.
        var request = ShareRequest.Create(TestUserId, TestGameId, ContributionType.NewGame, "With the rulebook");
        request.AttachDocument(Guid.NewGuid(), "rules.pdf", "application/pdf", 1024);
        request.StartReview(TestAdminId);
        GivenTheRepositoryReturns(request);

        var act = () => _handler.Handle(
            new ApproveShareRequestCommand(
                ShareRequestId: request.Id, AdminId: TestAdminId, TargetSharedGameId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*does not match*");
        request.Status.Should().Be(ShareRequestStatus.InReview);
        VerifyNoCopy();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheDomainRefusesTheApproval_CopiesNothing()
    {
        // Another admin holds the review: Approve throws, and no copied file must be left behind for a refused approval.
        var request = ShareRequest.Create(TestUserId, TestGameId, ContributionType.NewGame, "With the rulebook");
        request.AttachDocument(Guid.NewGuid(), "rules.pdf", "application/pdf", 1024);
        request.StartReview(Guid.NewGuid());
        GivenTheRepositoryReturns(request);

        var act = () => _handler.Handle(
            new ApproveShareRequestCommand(ShareRequestId: request.Id, AdminId: TestAdminId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ShareRequestReviewerMismatchException>();
        VerifyNoCopy();
    }

    [Fact]
    public async Task Handle_WhenADocumentCannotBeCopied_ThrowsConflict_AndSavesNothing()
    {
        // From the review of #5131: the copy service logs and skips a document it cannot copy. Approved anyway, the
        // request would close with that document left out, which is what the old 409 existed to prevent.
        var request = ShareRequest.Create(TestUserId, TestGameId, ContributionType.NewGame, "With the rulebook");
        request.AttachDocument(Guid.NewGuid(), "rules.pdf", "application/pdf", 1024);
        request.AttachDocument(Guid.NewGuid(), "faq.pdf", "application/pdf", 1024);
        request.StartReview(TestAdminId);
        GivenTheRepositoryReturns(request);
        _documentsMock
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });

        var act = () => _handler.Handle(
            new ApproveShareRequestCommand(ShareRequestId: request.Id, AdminId: TestAdminId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*1 of 2 attached documents*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void GivenTheRepositoryReturns(ShareRequest request) =>
        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

    private void VerifyCopiesInto(Guid sharedGameId, Times times) =>
        _documentsMock.Verify(d => d.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(), sharedGameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), times);

    private void VerifyNoCopy() =>
        _documentsMock.Verify(d => d.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    private static ShareRequest CreateShareRequestInReview(Guid reviewingAdminId)
    {
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            "Test notes");

        // Start review to put it in InReview status
        shareRequest.StartReview(reviewingAdminId);

        return shareRequest;
    }
}
