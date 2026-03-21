using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the SharedGameDeleteRequest entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 26
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameDeleteRequestTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsDeleteRequest()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();
        var reason = "Game is duplicate of existing entry";

        // Act
        var request = SharedGameDeleteRequest.Create(sharedGameId, requestedBy, reason);

        // Assert
        request.Id.Should().NotBe(Guid.Empty);
        request.SharedGameId.Should().Be(sharedGameId);
        request.RequestedBy.Should().Be(requestedBy);
        request.Reason.Should().Be(reason);
        request.Status.Should().Be(DeleteRequestStatus.Pending);
        request.ReviewedBy.Should().BeNull();
        request.ReviewComment.Should().BeNull();
        request.ReviewedAt.Should().BeNull();
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGameDeleteRequest.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "Valid reason");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*")
            .WithParameterName("sharedGameId");
    }

    [Fact]
    public void Create_WithEmptyRequestedBy_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "Valid reason");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*RequestedBy cannot be empty*")
            .WithParameterName("requestedBy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyReason_ThrowsArgumentException(string? reason)
    {
        // Act
        var action = () => SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            reason!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Reason is required*")
            .WithParameterName("reason");
    }

    [Fact]
    public void Create_WithReasonExceeding1000Characters_ThrowsArgumentException()
    {
        // Arrange
        var longReason = new string('R', 1001);

        // Act
        var action = () => SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longReason);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Reason cannot exceed 1000 characters*")
            .WithParameterName("reason");
    }

    [Fact]
    public void Create_WithReasonAt1000Characters_Succeeds()
    {
        // Arrange
        var reason = new string('R', 1000);

        // Act
        var request = SharedGameDeleteRequest.Create(Guid.NewGuid(), Guid.NewGuid(), reason);

        // Assert
        request.Reason.Should().HaveLength(1000);
    }

    #endregion

    #region Approve State Machine Tests

    [Fact]
    public void Approve_WhenPending_SetsStatusToApproved()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        var reviewerId = Guid.NewGuid();

        // Act
        request.Approve(reviewerId);

        // Assert
        request.Status.Should().Be(DeleteRequestStatus.Approved);
        request.ReviewedBy.Should().Be(reviewerId);
        request.ReviewedAt.Should().NotBeNull();
        request.ReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Approve_WithComment_SetsReviewComment()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Duplicate game");
        var reviewerId = Guid.NewGuid();
        var comment = "Confirmed duplicate, approved for deletion";

        // Act
        request.Approve(reviewerId, comment);

        // Assert
        request.Status.Should().Be(DeleteRequestStatus.Approved);
        request.ReviewComment.Should().Be(comment);
    }

    [Fact]
    public void Approve_WithoutComment_SetsNullReviewComment()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        var reviewerId = Guid.NewGuid();

        // Act
        request.Approve(reviewerId);

        // Assert
        request.ReviewComment.Should().BeNull();
    }

    [Fact]
    public void Approve_WithEmptyReviewedBy_ThrowsArgumentException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");

        // Act
        var action = () => request.Approve(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ReviewedBy cannot be empty*")
            .WithParameterName("reviewedBy");
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        request.Approve(Guid.NewGuid());

        // Act
        var action = () => request.Approve(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot approve request in Approved status*");
    }

    [Fact]
    public void Approve_WhenRejected_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        request.Reject(Guid.NewGuid(), "Not valid");

        // Act
        var action = () => request.Approve(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot approve request in Rejected status*");
    }

    #endregion

    #region Reject State Machine Tests

    [Fact]
    public void Reject_WhenPending_SetsStatusToRejected()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Delete request reason");
        var reviewerId = Guid.NewGuid();
        var reason = "Game is not a duplicate";

        // Act
        request.Reject(reviewerId, reason);

        // Assert
        request.Status.Should().Be(DeleteRequestStatus.Rejected);
        request.ReviewedBy.Should().Be(reviewerId);
        request.ReviewComment.Should().Be(reason);
        request.ReviewedAt.Should().NotBeNull();
        request.ReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Reject_WithEmptyReviewedBy_ThrowsArgumentException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");

        // Act
        var action = () => request.Reject(Guid.Empty, "Rejection reason");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ReviewedBy cannot be empty*")
            .WithParameterName("reviewedBy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithEmptyReason_ThrowsArgumentException(string? reason)
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");

        // Act
        var action = () => request.Reject(Guid.NewGuid(), reason!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Rejection reason is required*")
            .WithParameterName("reason");
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        request.Reject(Guid.NewGuid(), "First rejection");

        // Act
        var action = () => request.Reject(Guid.NewGuid(), "Second rejection");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot reject request in Rejected status*");
    }

    [Fact]
    public void Reject_WhenApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason");
        request.Approve(Guid.NewGuid());

        // Act
        var action = () => request.Reject(Guid.NewGuid(), "Too late");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot reject request in Approved status*");
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_CreatesDeleteRequestWithAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();
        var reason = "Test reason";
        var status = DeleteRequestStatus.Approved;
        var reviewedBy = Guid.NewGuid();
        var reviewComment = "Approved comment";
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var reviewedAt = DateTime.UtcNow.AddHours(-12);

        // Act
        var request = new SharedGameDeleteRequest(
            id,
            sharedGameId,
            requestedBy,
            reason,
            status,
            reviewedBy,
            reviewComment,
            createdAt,
            reviewedAt);

        // Assert
        request.Id.Should().Be(id);
        request.SharedGameId.Should().Be(sharedGameId);
        request.RequestedBy.Should().Be(requestedBy);
        request.Reason.Should().Be(reason);
        request.Status.Should().Be(status);
        request.ReviewedBy.Should().Be(reviewedBy);
        request.ReviewComment.Should().Be(reviewComment);
        request.CreatedAt.Should().Be(createdAt);
        request.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void InternalConstructor_WithPendingStatus_HasNullReviewFields()
    {
        // Act
        var request = new SharedGameDeleteRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test reason",
            DeleteRequestStatus.Pending,
            reviewedBy: null,
            reviewComment: null,
            DateTime.UtcNow,
            reviewedAt: null);

        // Assert
        request.Status.Should().Be(DeleteRequestStatus.Pending);
        request.ReviewedBy.Should().BeNull();
        request.ReviewComment.Should().BeNull();
        request.ReviewedAt.Should().BeNull();
    }

    #endregion
}
