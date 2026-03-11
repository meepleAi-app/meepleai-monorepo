using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", TestCategories.Unit)]
public class AddRagToSharedGameCommandValidatorTests
{
    private readonly AddRagToSharedGameCommandValidator _validator;

    public AddRagToSharedGameCommandValidatorTests()
    {
        _validator = new AddRagToSharedGameCommandValidator();
    }

    private static AddRagToSharedGameCommand CreateValidCommand(
        Guid? sharedGameId = null,
        Guid? userId = null,
        IFormFile? file = null,
        SharedGameDocumentType documentType = SharedGameDocumentType.Rulebook,
        string version = "1.0",
        List<string>? tags = null,
        bool isAdmin = true)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024); // 1KB
        fileMock.Setup(f => f.FileName).Returns("rulebook.pdf");

        return new AddRagToSharedGameCommand(
            SharedGameId: sharedGameId ?? Guid.NewGuid(),
            File: file ?? fileMock.Object,
            DocumentType: documentType,
            Version: version,
            Tags: tags,
            UserId: userId ?? Guid.NewGuid(),
            IsAdmin: isAdmin);
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_EmptySharedGameId_Fails()
    {
        // Arrange
        var command = CreateValidCommand(sharedGameId: Guid.Empty);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "SharedGameId is required");
    }

    [Fact]
    public async Task Validate_EmptyUserId_Fails()
    {
        // Arrange
        var command = CreateValidCommand(userId: Guid.Empty);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "UserId is required");
    }

    [Fact]
    public async Task Validate_NullFile_Fails()
    {
        // Arrange — must construct manually to pass null file
        var command = new AddRagToSharedGameCommand(
            SharedGameId: Guid.NewGuid(),
            File: null!,
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            UserId: Guid.NewGuid(),
            IsAdmin: true);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PDF file is required");
    }

    [Fact]
    public async Task Validate_FileTooLarge_Fails()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(51L * 1024 * 1024); // 51MB
        fileMock.Setup(f => f.FileName).Returns("huge.pdf");

        var command = CreateValidCommand(file: fileMock.Object);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "File size must not exceed 50MB");
    }

    [Fact]
    public async Task Validate_FileExactly50MB_Passes()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(50L * 1024 * 1024); // Exactly 50MB
        fileMock.Setup(f => f.FileName).Returns("exact.pdf");

        var command = CreateValidCommand(file: fileMock.Object);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyVersion_Fails(string version)
    {
        // Arrange
        var command = CreateValidCommand(version: version);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Version is required");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("v1.0")]
    [InlineData("1.")]
    [InlineData(".1")]
    public async Task Validate_InvalidVersionFormat_Fails(string version)
    {
        // Arrange
        var command = CreateValidCommand(version: version);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Version must be in format MAJOR.MINOR (e.g., 1.0, 2.1)");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("2.1")]
    [InlineData("10.99")]
    public async Task Validate_ValidVersionFormat_Passes(string version)
    {
        // Arrange
        var command = CreateValidCommand(version: version);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_InvalidDocumentType_Fails()
    {
        // Arrange
        var command = CreateValidCommand(documentType: (SharedGameDocumentType)99);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "DocumentType must be valid (Rulebook, Errata, or Homerule)");
    }

    [Fact]
    public async Task Validate_TagsOnRulebook_Fails()
    {
        // Arrange
        var command = CreateValidCommand(
            documentType: SharedGameDocumentType.Rulebook,
            tags: new List<string> { "tag1" });

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Tags are only allowed for Homerule documents");
    }

    [Fact]
    public async Task Validate_TagsOnHomerule_Passes()
    {
        // Arrange
        var command = CreateValidCommand(
            documentType: SharedGameDocumentType.Homerule,
            tags: new List<string> { "variant", "solo" });

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_TooManyTags_Fails()
    {
        // Arrange
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();
        var command = CreateValidCommand(
            documentType: SharedGameDocumentType.Homerule,
            tags: tags);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Cannot have more than 10 tags");
    }

    [Fact]
    public async Task Validate_EmptyTagInList_Fails()
    {
        // Arrange
        var command = CreateValidCommand(
            documentType: SharedGameDocumentType.Homerule,
            tags: new List<string> { "valid", "", "also-valid" });

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Each tag must be non-empty and not exceed 50 characters");
    }

    [Fact]
    public async Task Validate_TagTooLong_Fails()
    {
        // Arrange
        var longTag = new string('a', 51);
        var command = CreateValidCommand(
            documentType: SharedGameDocumentType.Homerule,
            tags: new List<string> { longTag });

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Each tag must be non-empty and not exceed 50 characters");
    }

    [Fact]
    public async Task Validate_NullTags_Passes()
    {
        // Arrange
        var command = CreateValidCommand(tags: null);

        // Act
        var result = await _validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }
}
