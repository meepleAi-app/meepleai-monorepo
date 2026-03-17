using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for the Priority parameter on UploadPdfCommand.
/// Validates priority parsing, validator behavior, and default resolution.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UploadPdfPriorityTests
{
    private readonly UploadPdfCommandValidator _validator = new();

    private static IFormFile CreateMockFile()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        return fileMock.Object;
    }

    #region Validator Tests

    [Fact]
    public void Validator_WithNullPriority_ShouldPass()
    {
        // Arrange
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: CreateMockFile(),
            Priority: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("urgent")]
    [InlineData("Normal")]
    [InlineData("URGENT")]
    public void Validator_WithValidPriority_ShouldPass(string priority)
    {
        // Arrange
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: CreateMockFile(),
            Priority: priority);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("high")]
    [InlineData("low")]
    [InlineData("critical")]
    [InlineData("")]
    public void Validator_WithInvalidPriority_ShouldFail(string priority)
    {
        // Arrange
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: CreateMockFile(),
            Priority: priority);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be 'normal' or 'urgent'");
    }

    [Fact]
    public void Validator_WithNullFile_ShouldFail()
    {
        // Arrange
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: null!,
            Priority: "urgent");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validator_WithEmptyUserId_ShouldFail()
    {
        // Arrange
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.Empty,
            File: CreateMockFile(),
            Priority: "normal");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region Priority Resolution Tests

    [Fact]
    public void UrgentPriority_MapsToProcessingPriorityUrgent()
    {
        // Arrange
        var requestedPriority = "urgent";

        // Act — same logic as UploadPdfCommandHandler
        var resolvedPriority = requestedPriority.ToLowerInvariant() == "urgent"
            ? ProcessingPriority.Urgent
            : ProcessingPriority.High;

        // Assert
        resolvedPriority.Should().Be(ProcessingPriority.Urgent);
        ((int)resolvedPriority).Should().Be(30);
    }

    [Fact]
    public void NullPriority_DefaultsToProcessingPriorityHigh()
    {
        // Arrange
        string? requestedPriority = null;

        // Act — same logic as UploadPdfCommandHandler
        var resolvedPriority = requestedPriority?.ToLowerInvariant() == "urgent"
            ? ProcessingPriority.Urgent
            : ProcessingPriority.High;

        // Assert
        resolvedPriority.Should().Be(ProcessingPriority.High);
        ((int)resolvedPriority).Should().Be(20);
    }

    [Fact]
    public void NormalPriority_DefaultsToProcessingPriorityHigh()
    {
        // Arrange — "normal" is a valid value but maps to High (admin default)
        var requestedPriority = "normal";

        // Act
        var resolvedPriority = requestedPriority.ToLowerInvariant() == "urgent"
            ? ProcessingPriority.Urgent
            : ProcessingPriority.High;

        // Assert
        resolvedPriority.Should().Be(ProcessingPriority.High);
        ((int)resolvedPriority).Should().Be(20);
    }

    #endregion

    #region Command Construction Tests

    [Fact]
    public void UploadPdfCommand_DefaultPriority_IsNull()
    {
        // Arrange & Act
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: CreateMockFile());

        // Assert — Priority defaults to null when not specified
        command.Priority.Should().BeNull();
    }

    [Fact]
    public void UploadPdfCommand_WithPriority_PreservesValue()
    {
        // Arrange & Act
        var command = new UploadPdfCommand(
            GameId: "game-1",
            Metadata: null,
            PrivateGameId: null,
            UserId: Guid.NewGuid(),
            File: CreateMockFile(),
            Priority: "urgent");

        // Assert
        command.Priority.Should().Be("urgent");
    }

    #endregion
}
