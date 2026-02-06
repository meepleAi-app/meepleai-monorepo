using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Unit tests for ProposePrivateGameCommandValidator.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class ProposePrivateGameCommandValidatorTests
{
    private readonly ProposePrivateGameCommandValidator _validator;

    public ProposePrivateGameCommandValidatorTests()
    {
        _validator = new ProposePrivateGameCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ReturnsValid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Please add this great game to the catalog");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_ReturnsInvalid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "Notes");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validate_EmptyPrivateGameId_ReturnsInvalid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.Empty,
            "Notes");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "PrivateGameId");
    }

    [Fact]
    public void Validate_NotesTooLong_ReturnsInvalid()
    {
        // Arrange
        var longNotes = new string('a', 2001); // 2001 characters
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longNotes);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Notes" &&
            e.ErrorMessage.Contains("2000"));
    }

    [Fact]
    public void Validate_TooManyDocuments_ReturnsInvalid()
    {
        // Arrange
        var tooManyDocs = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Notes",
            tooManyDocs);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "AttachedDocumentIds" &&
            e.ErrorMessage.Contains("10"));
    }

    [Fact]
    public void Validate_EmptyDocumentId_ReturnsInvalid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Notes",
            new List<Guid> { Guid.NewGuid(), Guid.Empty, Guid.NewGuid() });

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "AttachedDocumentIds" &&
            e.ErrorMessage.Contains("valid"));
    }

    [Fact]
    public void Validate_NullNotes_ReturnsValid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullDocumentIds_ReturnsValid()
    {
        // Arrange
        var command = new ProposePrivateGameCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Notes",
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
