using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for UpdateShareRequestDocumentsCommandValidator.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateShareRequestDocumentsCommandValidatorTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly UpdateShareRequestDocumentsCommandValidator _validator;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly Guid TestDocId = Guid.NewGuid();

    public UpdateShareRequestDocumentsCommandValidatorTests()
    {
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();

        // Default setup: documents belong to user
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, CancellationToken _) =>
                ids.Select(id => CreateMockDocument(id, TestUserId)).ToList());

        _validator = new UpdateShareRequestDocumentsCommandValidator(
            _documentRepositoryMock.Object);
    }

    [Fact]
    public async Task Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            TestUserId,
            new List<Guid> { TestDocId });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithEmptyShareRequestId_FailsValidation()
    {
        // Arrange
        var command = new UpdateShareRequestDocumentsCommand(
            Guid.Empty,
            TestUserId,
            new List<Guid> { TestDocId });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareRequestId);
    }

    [Fact]
    public async Task Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            Guid.Empty,
            new List<Guid> { TestDocId });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_WithTooManyDocuments_FailsValidation()
    {
        // Arrange
        var tooManyDocs = Enumerable.Range(0, ShareRequest.MaxDocumentCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            TestUserId,
            tooManyDocs);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DocumentIds);
    }

    [Fact]
    public async Task Validate_WithDocumentsNotOwnedByUser_FailsValidation()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            TestUserId,
            new List<Guid> { TestDocId });

        // Setup: document belongs to different user
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument>
            {
                CreateMockDocument(TestDocId, otherUserId)
            });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public async Task Validate_WithNonExistentDocument_FailsValidation()
    {
        // Arrange
        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            TestUserId,
            new List<Guid> { TestDocId, Guid.NewGuid() });

        // Setup: only one document exists
        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfDocument>
            {
                CreateMockDocument(TestDocId, TestUserId)
            });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x);
    }

    private static PdfDocument CreateMockDocument(Guid id, Guid userId)
    {
        return new PdfDocument(
            id,
            TestGameId,
            new FileName("test.pdf"),
            "test/path.pdf",
            new FileSize(1024),
            userId);
    }
}
