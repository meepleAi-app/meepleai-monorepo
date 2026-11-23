using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Tests for BulkApproveShareRequestsCommandHandler.
/// Issue #2893: Verify all-or-nothing transaction and audit logging.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkApproveShareRequestsCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _mockShareRequestRepository;
    private readonly Mock<IShareRequestDocumentService> _mockDocuments;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly BulkApproveShareRequestsCommandHandler _handler;

    public BulkApproveShareRequestsCommandHandlerTests()
    {
        _mockShareRequestRepository = new Mock<IShareRequestRepository>();
        _mockDocuments = new Mock<IShareRequestDocumentService>();
        // As the real service when every copy succeeds: one new document id for each source.
        _mockDocuments
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, Guid _, Guid _, CancellationToken _) => ids.Select(_ => Guid.NewGuid()).ToList());
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new BulkApproveShareRequestsCommandHandler(
            _mockShareRequestRepository.Object,
            _mockDocuments.Object,
            Mock.Of<IContributorRepository>(),
            _mockAuditLogRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<BulkApproveShareRequestsCommandHandler>>(),
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithAllValidRequests_ApprovesAllSuccessfully()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        var shareRequestsDict = shareRequestIds.ToDictionary(
            id => id,
            id => CreateInReviewShareRequest(id, editorId));

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.SuccessCount.Should().Be(shareRequestIds.Count);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
        _mockShareRequestRepository.Verify(
            r => r.Update(It.IsAny<ShareRequest>()),
            Times.Exactly(shareRequestIds.Count));
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllValidRequests_CreatesAuditLogForEach()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        var shareRequestsDict = shareRequestIds.ToDictionary(
            id => id,
            id => CreateInReviewShareRequest(id, editorId));

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockAuditLogRepository.Verify(
            r => r.AddAsync(
                It.Is<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(a =>
                    a.UserId == editorId &&
                    a.Action == "share_request_approved" &&
                    a.Resource == "ShareRequest" &&
                    a.Result == "success"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(shareRequestIds.Count));
    }

    [Fact]
    public async Task Handle_WhenAnyRequestNotFound_RollsBackAllChanges()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        // Only first two exist in dictionary, third missing
        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            { shareRequestIds[0], CreateInReviewShareRequest(shareRequestIds[0], editorId) },
            { shareRequestIds[1], CreateInReviewShareRequest(shareRequestIds[1], editorId) }
            // shareRequestIds[2] is missing - triggers rollback
        };

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All-or-nothing failure
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(shareRequestIds.Count);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Bulk operation failed");
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never); // No commit on failure
    }

    [Fact]
    public async Task Handle_WhenDuplicateIds_RemovesDuplicatesWithWarning()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var shareRequestIds = new List<Guid> { id1, id2, id1 }; // id1 duplicated
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            { id1, CreateInReviewShareRequest(id1, editorId) },
            { id2, CreateInReviewShareRequest(id2, editorId) }
        };

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken ct) => shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Only 2 distinct IDs processed
        result.SuccessCount.Should().Be(2);
        _mockShareRequestRepository.Verify(
            r => r.GetByIdsForUpdateAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Batch method called once for all IDs
    }

    [Fact]
    public async Task Handle_WhenExceedsMaxLimit_ThrowsDomainException()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var shareRequestIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkApproveShareRequestsCommand(shareRequestIds, editorId);

        // Act & Assert
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Handler catches DomainException and returns failure result
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(shareRequestIds.Count);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("maximum limit of 20");
    }

    // #5074: a NewGameProposal in the batch fails the whole batch (all-or-nothing), and nothing is saved.
    [Fact]
    public async Task Handle_WithANewGameProposal_FailsTheBatch_AndSavesNothing()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var regular = CreateInReviewShareRequest(Guid.NewGuid(), editorId);
        var proposal = ShareRequest.CreateGameProposal(Guid.NewGuid(), Guid.NewGuid(), "Please add it");
        proposal.StartReview(editorId, lockDurationMinutes: 30);
        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            [regular.Id] = regular,
            [proposal.Id] = proposal,
        };
        var command = new BulkApproveShareRequestsCommand([regular.Id, proposal.Id], editorId);

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(2);
        result.Errors.Should().ContainSingle().Which.Should().Contain("approve-game-proposal");
        proposal.Status.Should().Be(ShareRequestStatus.InReview);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // #5109, decision of 2026-11-23: each request's documents go to the game that request contributes to.
    [Fact]
    public async Task Handle_CopiesTheDocumentsOfEachRequest_IntoItsOwnDestination_Once()
    {
        var editorId = Guid.NewGuid();
        var newGame = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        var existingGameId = Guid.NewGuid();
        var expansion = WithDocument(
            ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.AdditionalContent, "Expansion", existingGameId),
            editorId);
        GivenTheBatch(newGame, expansion);

        var result = await _handler.Handle(
            new BulkApproveShareRequestsCommand([newGame.Id, expansion.Id], editorId),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(2);
        VerifyCopiesInto(newGame.SourceGameId, Times.Once());
        VerifyCopiesInto(existingGameId, Times.Once());
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithABatchTargetNamingAnotherGame_FailsTheBatch_AndCopiesNothing()
    {
        // One TargetSharedGameId for the whole batch: honoured, it would pour the documents of different games into one.
        var editorId = Guid.NewGuid();
        var first = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        var second = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        GivenTheBatch(first, second);

        var result = await _handler.Handle(
            new BulkApproveShareRequestsCommand([first.Id, second.Id], editorId, TargetSharedGameId: first.SourceGameId),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("does not match");
        VerifyNoCopy();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenALaterRequestIsRefused_CopiesNothing_ForTheOnesBeforeIt()
    {
        // The copies wait for the whole batch to be approved: the second request's review belongs to another editor.
        var editorId = Guid.NewGuid();
        var first = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        var second = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), Guid.NewGuid());
        GivenTheBatch(first, second);

        var result = await _handler.Handle(
            new BulkApproveShareRequestsCommand([first.Id, second.Id], editorId),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(0);
        VerifyNoCopy();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenADocumentCannotBeCopied_FailsTheBatch_AndSavesNothing()
    {
        var editorId = Guid.NewGuid();
        var first = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        var second = WithDocument(ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame, "Rules"), editorId);
        GivenTheBatch(first, second);
        _mockDocuments
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), second.SourceGameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var result = await _handler.Handle(
            new BulkApproveShareRequestsCommand([first.Id, second.Id], editorId),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("0 of 1 attached documents");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShareRequest WithDocument(ShareRequest request, Guid reviewerId)
    {
        request.AttachDocument(Guid.NewGuid(), "rules.pdf", "application/pdf", 1024);
        request.StartReview(reviewerId, lockDurationMinutes: 30);
        return request;
    }

    private void GivenTheBatch(params ShareRequest[] requests) =>
        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.ToDictionary(r => r.Id));

    private void VerifyCopiesInto(Guid sharedGameId, Times times) =>
        _mockDocuments.Verify(d => d.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(), sharedGameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), times);

    private void VerifyNoCopy() =>
        _mockDocuments.Verify(d => d.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    // #5076: a CoverChange in the batch fails it too: approved here, its pending cover never reached the game.
    [Fact]
    public async Task Handle_WithACoverChange_FailsTheBatch_AndSavesNothing()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var regular = CreateInReviewShareRequest(Guid.NewGuid(), editorId);
        var coverChange = ShareRequest.CreateCoverChange(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "covers/g/pdf-cover", 3, null);
        coverChange.StartReview(editorId, lockDurationMinutes: 30);
        var shareRequestsDict = new Dictionary<Guid, ShareRequest>
        {
            [regular.Id] = regular,
            [coverChange.Id] = coverChange,
        };
        var command = new BulkApproveShareRequestsCommand([regular.Id, coverChange.Id], editorId);

        _mockShareRequestRepository
            .Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequestsDict);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(2);
        result.Errors.Should().ContainSingle().Which.Should().Contain("CoverChange");
        coverChange.Status.Should().Be(ShareRequestStatus.InReview);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShareRequest CreateInReviewShareRequest(Guid id, Guid editorId)
    {
        var shareRequest = ShareRequest.Create(
            userId: Guid.NewGuid(),
            sourceGameId: Guid.NewGuid(),
            contributionType: ContributionType.NewGame,
            userNotes: "Test share request");

        // Transition to InReview status
        shareRequest.StartReview(editorId, lockDurationMinutes: 30);

        return shareRequest;
    }
}
