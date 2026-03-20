using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CreateShareRequestCommandValidatorTests
{
    private readonly Mock<IUserLibraryRepository> _userLibraryRepositoryMock;
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IRateLimitEvaluator> _rateLimitEvaluatorMock;
    private readonly CreateShareRequestCommandValidator _validator;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public CreateShareRequestCommandValidatorTests()
    {
        _userLibraryRepositoryMock = new Mock<IUserLibraryRepository>();
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _rateLimitEvaluatorMock = new Mock<IRateLimitEvaluator>();

        // Default setup: allow everything
        SetupDefaultMocks();

        _validator = new CreateShareRequestCommandValidator(
            _userLibraryRepositoryMock.Object,
            _shareRequestRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _rateLimitEvaluatorMock.Object);
    }

    private void SetupDefaultMocks()
    {
        _userLibraryRepositoryMock
            .Setup(r => r.IsGameInLibraryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _shareRequestRepositoryMock
            .Setup(r => r.HasPendingRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _rateLimitEvaluatorMock
            .Setup(r => r.CanUserCreateRequestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument>());
    }

    [Fact]
    public async Task Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            Notes: "This is a great game!");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: Guid.Empty,
            SourceGameId: TestGameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public async Task Validate_WithEmptySourceGameId_FailsValidation()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: Guid.Empty);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SourceGameId)
            .WithErrorMessage("SourceGameId is required");
    }

    [Fact]
    public async Task Validate_WhenUserDoesNotOwnGame_FailsValidation()
    {
        // Arrange
        _userLibraryRepositoryMock
            .Setup(r => r.IsGameInLibraryAsync(TestUserId, TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == "You can only share games from your library");
    }

    [Fact]
    public async Task Validate_WithPendingRequest_FailsValidation()
    {
        // Arrange
        _shareRequestRepositoryMock
            .Setup(r => r.HasPendingRequestAsync(TestUserId, TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == "You already have a pending share request for this game");
    }

    [Fact]
    public async Task Validate_WhenRateLimitExceeded_FailsValidation()
    {
        // Arrange
        _rateLimitEvaluatorMock
            .Setup(r => r.CanUserCreateRequestAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.Errors.Should().Contain(e => e.ErrorMessage == "Rate limit exceeded. Please wait before submitting new requests.");
    }

    [Fact]
    public async Task Validate_WithNotesTooLong_FailsValidation()
    {
        // Arrange
        var longNotes = new string('a', 2001);
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            Notes: longNotes);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 2000 characters");
    }

    [Fact]
    public async Task Validate_WithNotesExactlyAtLimit_PassesValidation()
    {
        // Arrange
        var exactNotes = new string('a', 2000);
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            Notes: exactNotes);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public async Task Validate_WithDocumentsNotOwned_FailsValidation()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var document = CreateMockPdfDocument(docId, otherUserId, "rules.pdf", 1024);
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<List<Guid>>(ids => ids.Contains(docId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: [docId]);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AttachedDocumentIds)
            .WithErrorMessage("You can only attach documents you own");
    }

    [Fact]
    public async Task Validate_WithNonExistentDocument_FailsValidation()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument>()); // Return empty = document not found

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: [docId]);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AttachedDocumentIds)
            .WithErrorMessage("You can only attach documents you own");
    }

    [Fact]
    public async Task Validate_WithTooManyDocuments_FailsValidation()
    {
        // Arrange
        var documentIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: documentIds);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AttachedDocumentIds)
            .WithErrorMessage("Cannot attach more than 10 documents");
    }

    [Fact]
    public async Task Validate_WithValidOwnedDocuments_PassesValidation()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var document = CreateMockPdfDocument(docId, TestUserId, "rules.pdf", 1024);
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.Is<List<Guid>>(ids => ids.Contains(docId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: [docId]);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AttachedDocumentIds);
    }

    [Fact]
    public async Task Validate_WithEmptyDocumentList_PassesValidation()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: []);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AttachedDocumentIds);
    }

    [Fact]
    public async Task Validate_WithNullNotes_PassesValidation()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            Notes: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    private static PdfDocument CreateMockPdfDocument(Guid docId, Guid userId, string fileName, long fileSize)
    {
        // Create PdfDocument using reflection to set properties
#pragma warning disable SYSLIB0050 // FormatterServices is obsolete but required for test instantiation of sealed classes
        var doc = (PdfDocument)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PdfDocument));
#pragma warning restore SYSLIB0050

        // Use reflection to set the internal _id field from base class
        var idField = doc.GetType().BaseType?.GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(doc, docId);

        // Set FileName using the property
        var fileNameProperty = typeof(PdfDocument).GetProperty("FileName");
        fileNameProperty?.SetValue(doc, new FileName(fileName));

        // Set ContentType
        var contentTypeProperty = typeof(PdfDocument).GetProperty("ContentType");
        contentTypeProperty?.SetValue(doc, "application/pdf");

        // Set FileSize
        var fileSizeProperty = typeof(PdfDocument).GetProperty("FileSize");
        fileSizeProperty?.SetValue(doc, new FileSize(fileSize));

        // Set UploadedByUserId
        var uploaderProperty = typeof(PdfDocument).GetProperty("UploadedByUserId");
        uploaderProperty?.SetValue(doc, userId);

        return doc;
    }
}