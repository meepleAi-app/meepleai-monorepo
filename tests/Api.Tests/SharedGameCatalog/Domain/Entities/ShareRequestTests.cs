using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Domain.Entities;

public sealed class ShareRequestTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sourceGameId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _targetSharedGameId = Guid.NewGuid();

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_CreatesNewGameRequest()
    {
        // Act
        var request = ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.NewGame,
            "Test notes");

        // Assert
        request.Should().NotBeNull();
        request.Id.Should().NotBe(Guid.Empty);
        request.UserId.Should().Be(_userId);
        request.SourceGameId.Should().Be(_sourceGameId);
        request.ContributionType.Should().Be(ContributionType.NewGame);
        request.Status.Should().Be(ShareRequestStatus.Pending);
        request.UserNotes.Should().Be("Test notes");
        request.TargetSharedGameId.Should().BeNull();
        request.ReviewingAdminId.Should().BeNull();
        request.AdminFeedback.Should().BeNull();
        request.ResolvedAt.Should().BeNull();
        request.CreatedBy.Should().Be(_userId);
    }

    [Fact]
    public void Create_WithValidParameters_CreatesAdditionalContentRequest()
    {
        // Act
        var request = ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.AdditionalContent,
            "Additional content notes",
            _targetSharedGameId);

        // Assert
        request.Should().NotBeNull();
        request.ContributionType.Should().Be(ContributionType.AdditionalContent);
        request.TargetSharedGameId.Should().Be(_targetSharedGameId);
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        // Act
        var request = ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.NewGame);

        // Assert
        request.DomainEvents.Should().ContainSingle();
        var domainEvent = request.DomainEvents.First();
        domainEvent.Should().BeOfType<ShareRequestCreatedEvent>();
        var createdEvent = (ShareRequestCreatedEvent)domainEvent;
        createdEvent.ShareRequestId.Should().Be(request.Id);
        createdEvent.UserId.Should().Be(_userId);
        createdEvent.SourceGameId.Should().Be(_sourceGameId);
        createdEvent.ContributionType.Should().Be(ContributionType.NewGame);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequest.Create(
            Guid.Empty,
            _sourceGameId,
            ContributionType.NewGame);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Create_WithEmptySourceGameId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequest.Create(
            _userId,
            Guid.Empty,
            ContributionType.NewGame);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("sourceGameId");
    }

    [Fact]
    public void Create_AdditionalContentWithoutTargetId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.AdditionalContent);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("targetSharedGameId");
    }

    [Fact]
    public void Create_NewGameWithTargetId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.NewGame,
            null,
            _targetSharedGameId);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("targetSharedGameId");
    }

    [Fact]
    public void Create_WithTooLongUserNotes_ThrowsArgumentException()
    {
        // Arrange
        var longNotes = new string('x', 2001);

        // Act & Assert
        var action = () => ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.NewGame,
            longNotes);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("userNotes");
    }

    #endregion

    #region StartReview Tests

    [Fact]
    public void StartReview_FromPending_TransitionsToInReview()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.ClearDomainEvents();

        // Act
        request.StartReview(_adminId);

        // Assert
        request.Status.Should().Be(ShareRequestStatus.InReview);
        request.ReviewingAdminId.Should().Be(_adminId);
        request.ReviewStartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestReviewStartedEvent>();
    }

    [Fact]
    public void StartReview_FromChangesRequested_TransitionsToInReview()
    {
        // Arrange
        var request = CreateRequestInChangesRequestedState();
        request.ClearDomainEvents();

        // Act
        request.StartReview(_adminId);

        // Assert
        request.Status.Should().Be(ShareRequestStatus.InReview);
        request.ReviewingAdminId.Should().Be(_adminId);
    }

    [Fact]
    public void StartReview_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);

        // Act & Assert
        var action = () => request.StartReview(Guid.Empty);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("adminId");
    }

    [Fact]
    public void StartReview_WhenAlreadyInReview_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var anotherAdmin = Guid.NewGuid();

        // Act & Assert
        // When already InReview, the status check fails first with "InReview status" message
        var action = () => request.StartReview(anotherAdmin);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*InReview*");
    }

    [Theory]
    [InlineData(ShareRequestStatus.Approved)]
    [InlineData(ShareRequestStatus.Rejected)]
    [InlineData(ShareRequestStatus.Withdrawn)]
    public void StartReview_FromTerminalState_ThrowsInvalidOperationException(ShareRequestStatus terminalStatus)
    {
        // Arrange
        var request = CreateRequestInState(terminalStatus);

        // Act & Assert
        var action = () => request.StartReview(_adminId);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{terminalStatus}*");
    }

    #endregion

    #region ReleaseReview Tests

    [Fact]
    public void ReleaseReview_FromInReview_TransitionsToPending()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        request.ClearDomainEvents();

        // Act
        request.ReleaseReview();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Pending);
        request.ReviewingAdminId.Should().BeNull();
        request.ReviewStartedAt.Should().BeNull();
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestReviewReleasedEvent>();
    }

    [Fact]
    public void ReleaseReview_NotInReview_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);

        // Act & Assert
        var action = () => request.ReleaseReview();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    #endregion

    #region Approve Tests

    [Fact]
    public void Approve_FromInReview_TransitionsToApproved()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        request.ClearDomainEvents();
        var newGameId = Guid.NewGuid();

        // Act
        request.Approve(_adminId, newGameId, "Great contribution!");

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Approved);
        request.TargetSharedGameId.Should().Be(newGameId);
        request.AdminFeedback.Should().Be("Great contribution!");
        request.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.DomainEvents.Should().ContainSingle();
        var approvedEvent = request.DomainEvents.First().Should().BeOfType<ShareRequestApprovedEvent>().Subject;
        approvedEvent.TargetSharedGameId.Should().Be(newGameId);
    }

    [Fact]
    public void Approve_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.Approve(Guid.Empty);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("adminId");
    }

    [Fact]
    public void Approve_NotInReview_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);

        // Act & Assert
        var action = () => request.Approve(_adminId);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Approve_ByDifferentAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var differentAdmin = Guid.NewGuid();

        // Act & Assert
        var action = () => request.Approve(differentAdmin);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reviewing admin*");
    }

    [Fact]
    public void Approve_WithTooLongFeedback_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var longFeedback = new string('x', 2001);

        // Act & Assert
        var action = () => request.Approve(_adminId, null, longFeedback);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("feedback");
    }

    #endregion

    #region Reject Tests

    [Fact]
    public void Reject_FromInReview_TransitionsToRejected()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        request.ClearDomainEvents();

        // Act
        request.Reject(_adminId, "Does not meet quality standards");

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Rejected);
        request.AdminFeedback.Should().Be("Does not meet quality standards");
        request.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestRejectedEvent>();
    }

    [Fact]
    public void Reject_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.Reject(Guid.Empty, "reason");
        action.Should().Throw<ArgumentException>()
            .WithParameterName("adminId");
    }

    [Fact]
    public void Reject_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.Reject(_adminId, "");
        action.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void Reject_WithTooLongReason_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var longReason = new string('x', 2001);

        // Act & Assert
        var action = () => request.Reject(_adminId, longReason);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void Reject_ByDifferentAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var differentAdmin = Guid.NewGuid();

        // Act & Assert
        var action = () => request.Reject(differentAdmin, "reason");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reviewing admin*");
    }

    #endregion

    #region RequestChanges Tests

    [Fact]
    public void RequestChanges_FromInReview_TransitionsToChangesRequested()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        request.ClearDomainEvents();

        // Act
        request.RequestChanges(_adminId, "Please add more details");

        // Assert
        request.Status.Should().Be(ShareRequestStatus.ChangesRequested);
        request.AdminFeedback.Should().Be("Please add more details");
        request.ReviewingAdminId.Should().BeNull();
        request.ReviewStartedAt.Should().BeNull();
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestChangesRequestedEvent>();
    }

    [Fact]
    public void RequestChanges_WithEmptyFeedback_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.RequestChanges(_adminId, "");
        action.Should().Throw<ArgumentException>()
            .WithParameterName("feedback");
    }

    [Fact]
    public void RequestChanges_ByDifferentAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();
        var differentAdmin = Guid.NewGuid();

        // Act & Assert
        var action = () => request.RequestChanges(differentAdmin, "feedback");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reviewing admin*");
    }

    #endregion

    #region Resubmit Tests

    [Fact]
    public void Resubmit_FromChangesRequested_TransitionsToPending()
    {
        // Arrange
        var request = CreateRequestInChangesRequestedState();
        request.ClearDomainEvents();

        // Act
        request.Resubmit("Updated notes");

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Pending);
        request.UserNotes.Should().Be("Updated notes");
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestResubmittedEvent>();
    }

    [Fact]
    public void Resubmit_NotInChangesRequestedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);

        // Act & Assert
        var action = () => request.Resubmit();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void Resubmit_WithTooLongNotes_ThrowsArgumentException()
    {
        // Arrange
        var request = CreateRequestInChangesRequestedState();
        var longNotes = new string('x', 2001);

        // Act & Assert
        var action = () => request.Resubmit(longNotes);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("updatedNotes");
    }

    #endregion

    #region Withdraw Tests

    [Fact]
    public void Withdraw_FromPending_TransitionsToWithdrawn()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.ClearDomainEvents();

        // Act
        request.Withdraw();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Withdrawn);
        request.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.DomainEvents.Should().ContainSingle();
        request.DomainEvents.First().Should().BeOfType<ShareRequestWithdrawnEvent>();
    }

    [Fact]
    public void Withdraw_FromChangesRequested_TransitionsToWithdrawn()
    {
        // Arrange
        var request = CreateRequestInChangesRequestedState();
        request.ClearDomainEvents();

        // Act
        request.Withdraw();

        // Assert
        request.Status.Should().Be(ShareRequestStatus.Withdrawn);
    }

    [Theory]
    [InlineData(ShareRequestStatus.Approved)]
    [InlineData(ShareRequestStatus.Rejected)]
    public void Withdraw_FromTerminalState_ThrowsInvalidOperationException(ShareRequestStatus terminalStatus)
    {
        // Arrange
        var request = CreateRequestInState(terminalStatus);

        // Act & Assert
        var action = () => request.Withdraw();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{terminalStatus}*");
    }

    [Fact]
    public void Withdraw_FromInReview_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.Withdraw();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*InReview*");
    }

    #endregion

    #region Document Tests

    [Fact]
    public void AttachDocument_ToPendingRequest_AddsDocument()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        var documentId = Guid.NewGuid();

        // Act
        request.AttachDocument(documentId, "rules.pdf", "application/pdf", 1024);

        // Assert
        request.AttachedDocuments.Should().ContainSingle();
        var doc = request.AttachedDocuments.First();
        doc.DocumentId.Should().Be(documentId);
        doc.FileName.Should().Be("rules.pdf");
        doc.ContentType.Should().Be("application/pdf");
        doc.FileSize.Should().Be(1024);
    }

    [Fact]
    public void AttachDocument_ToChangesRequestedRequest_AddsDocument()
    {
        // Arrange
        var request = CreateRequestInChangesRequestedState();
        var documentId = Guid.NewGuid();

        // Act
        request.AttachDocument(documentId, "updated.pdf", "application/pdf", 2048);

        // Assert
        request.AttachedDocuments.Should().ContainSingle();
    }

    [Fact]
    public void AttachDocument_ToInReviewRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequestInReviewState();

        // Act & Assert
        var action = () => request.AttachDocument(Guid.NewGuid(), "test.pdf", "application/pdf", 1024);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*InReview*");
    }

    [Fact]
    public void RemoveDocument_FromPendingRequest_RemovesDocument()
    {
        // Arrange
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        var documentId = Guid.NewGuid();
        request.AttachDocument(documentId, "rules.pdf", "application/pdf", 1024);

        // Act
        request.RemoveDocument(documentId);

        // Assert
        request.AttachedDocuments.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void CanBeReviewed_WhenPending_ReturnsTrue()
    {
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.CanBeReviewed().Should().BeTrue();
    }

    [Fact]
    public void CanBeReviewed_WhenChangesRequested_ReturnsTrue()
    {
        var request = CreateRequestInChangesRequestedState();
        request.CanBeReviewed().Should().BeTrue();
    }

    [Fact]
    public void CanBeReviewed_WhenInReview_ReturnsFalse()
    {
        var request = CreateRequestInReviewState();
        request.CanBeReviewed().Should().BeFalse();
    }

    [Fact]
    public void IsLockedForReview_WhenInReview_ReturnsTrue()
    {
        var request = CreateRequestInReviewState();
        request.IsLockedForReview().Should().BeTrue();
    }

    [Fact]
    public void IsLockedForReview_WhenPending_ReturnsFalse()
    {
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.IsLockedForReview().Should().BeFalse();
    }

    [Fact]
    public void IsResolved_WhenApproved_ReturnsTrue()
    {
        var request = CreateRequestInState(ShareRequestStatus.Approved);
        request.IsResolved().Should().BeTrue();
    }

    [Fact]
    public void IsResolved_WhenPending_ReturnsFalse()
    {
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.IsResolved().Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private ShareRequest CreateRequestInReviewState()
    {
        var request = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame);
        request.StartReview(_adminId);
        return request;
    }

    private ShareRequest CreateRequestInChangesRequestedState()
    {
        var request = CreateRequestInReviewState();
        request.RequestChanges(_adminId, "Please improve");
        return request;
    }

    private ShareRequest CreateRequestInState(ShareRequestStatus status)
    {
        var request = CreateRequestInReviewState();

        switch (status)
        {
            case ShareRequestStatus.Approved:
                request.Approve(_adminId);
                break;
            case ShareRequestStatus.Rejected:
                request.Reject(_adminId, "Not suitable");
                break;
            case ShareRequestStatus.Withdrawn:
                request.ReleaseReview();
                request.Withdraw();
                break;
            case ShareRequestStatus.ChangesRequested:
                request.RequestChanges(_adminId, "Please improve");
                break;
            default:
                break;
        }

        return request;
    }

    #endregion
}
