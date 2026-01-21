using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ReviewShareRequestValidatorTests
{
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public ReviewShareRequestValidatorTests()
    {
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
    }

    #region ApproveShareRequestCommandValidator Tests

    [Fact]
    public async Task ApproveValidator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            AdminNotes: "Approved!");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task ApproveValidator_WithEmptyShareRequestId_FailsValidation()
    {
        // Arrange
        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: Guid.Empty,
            AdminId: TestAdminId);

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareRequestId)
            .WithErrorMessage("ShareRequestId is required");
    }

    [Fact]
    public async Task ApproveValidator_WithEmptyAdminId_FailsValidation()
    {
        // Arrange
        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.Empty);

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AdminId)
            .WithErrorMessage("AdminId is required");
    }

    [Fact]
    public async Task ApproveValidator_WithNotesTooLong_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            AdminNotes: new string('a', 2001));

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AdminNotes)
            .WithErrorMessage("Admin notes cannot exceed 2000 characters");
    }

    [Fact]
    public async Task ApproveValidator_WithNonExistentShareRequest_FailsValidation()
    {
        // Arrange
        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId);

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Share request not found or not in review status");
    }

    [Fact]
    public async Task ApproveValidator_WithWrongReviewer_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var differentAdminId = Guid.NewGuid();
        var validator = new ApproveShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId); // Wrong admin

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        Assert.Contains(result.Errors, e => e.ErrorMessage == "You are not the current reviewer of this share request");
    }

    #endregion

    #region RejectShareRequestCommandValidator Tests

    [Fact]
    public async Task RejectValidator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RejectShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Reason: "This game does not meet our quality standards.");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RejectValidator_WithEmptyReason_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RejectShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Reason: "");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Rejection reason is required");
    }

    [Fact]
    public async Task RejectValidator_WithReasonTooShort_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RejectShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Reason: "Bad"); // Too short

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Please provide a meaningful reason (at least 10 characters)");
    }

    [Fact]
    public async Task RejectValidator_WithReasonTooLong_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RejectShareRequestCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Reason: new string('a', 2001));

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Reason cannot exceed 2000 characters");
    }

    #endregion

    #region RequestShareRequestChangesCommandValidator Tests

    [Fact]
    public async Task RequestChangesValidator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RequestShareRequestChangesCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "Please add better images and more details.");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RequestChangesValidator_WithEmptyFeedback_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RequestShareRequestChangesCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Feedback)
            .WithErrorMessage("Feedback is required when requesting changes");
    }

    [Fact]
    public async Task RequestChangesValidator_WithFeedbackTooShort_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RequestShareRequestChangesCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "Fix it"); // Too short

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Feedback)
            .WithErrorMessage("Please provide meaningful feedback (at least 10 characters)");
    }

    [Fact]
    public async Task RequestChangesValidator_WithFeedbackTooLong_FailsValidation()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        SetupRepositoryMock(shareRequest);

        var validator = new RequestShareRequestChangesCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: new string('a', 2001));

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Feedback)
            .WithErrorMessage("Feedback cannot exceed 2000 characters");
    }

    [Fact]
    public async Task RequestChangesValidator_WithShareRequestNotInReview_FailsValidation()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            "Test notes");
        // Note: NOT putting it in review, so it's in Pending status

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var validator = new RequestShareRequestChangesCommandValidator(_shareRequestRepositoryMock.Object);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "Please update the description.");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Share request not found or not in review status");
    }

    #endregion

    #region Helper Methods

    private void SetupRepositoryMock(ShareRequest shareRequest)
    {
        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
    }

    private static ShareRequest CreateShareRequestInReview(Guid reviewingAdminId)
    {
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            "Test notes");

        shareRequest.StartReview(reviewingAdminId);

        return shareRequest;
    }

    #endregion
}
