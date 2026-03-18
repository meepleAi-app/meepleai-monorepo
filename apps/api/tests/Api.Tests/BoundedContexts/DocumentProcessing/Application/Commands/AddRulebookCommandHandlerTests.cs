using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Services;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Unit tests for AddRulebookCommandHandler — PDF deduplication with upload-or-reuse branching.
/// Tests the three main branches: hash match (Ready/Processing/Failed) and no match (new upload).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AddRulebookCommandHandlerTests : IDisposable
{
    private readonly Mock<IPdfDocumentRepository> _pdfDocumentRepository;
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<IBlobStorageService> _blobStorageService;
    private readonly Mock<ITierEnforcementService> _tierEnforcementService;
    private readonly Mock<IBackgroundTaskService> _backgroundTaskService;
    private readonly Mock<IPdfUploadQuotaService> _quotaService;
    private readonly Mock<ILogger<AddRulebookCommandHandler>> _logger;
    private readonly AddRulebookCommandHandler _handler;

    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestPdfId = Guid.NewGuid();

    public AddRulebookCommandHandlerTests()
    {
        _pdfDocumentRepository = new Mock<IPdfDocumentRepository>();
        _db = TestDbContextFactory.CreateInMemoryDbContext();

        // Seed game and library entry for ownership validation
        _db.Games.Add(new GameEntity { Id = TestGameId, Name = "Test Game" });
        _db.UserLibraryEntries.Add(new UserLibraryEntryEntity { UserId = TestUserId, SharedGameId = TestGameId });
        _db.SaveChanges();

        _mediator = new Mock<IMediator>();
        _blobStorageService = new Mock<IBlobStorageService>();
        _tierEnforcementService = new Mock<ITierEnforcementService>();
        _backgroundTaskService = new Mock<IBackgroundTaskService>();
        _quotaService = new Mock<IPdfUploadQuotaService>();
        _logger = new Mock<ILogger<AddRulebookCommandHandler>>();

        _handler = new AddRulebookCommandHandler(
            _pdfDocumentRepository.Object,
            _db,
            _mediator.Object,
            _blobStorageService.Object,
            _tierEnforcementService.Object,
            _backgroundTaskService.Object,
            _quotaService.Object,
            _logger.Object);
    }

    public void Dispose() => _db.Dispose();

    #region Hash Match — Ready State

    [Fact]
    public async Task Handle_WhenHashMatchReady_ReturnsReusedWithReadyStatus()
    {
        // Arrange
        var existingDoc = CreatePdfDocument(PdfProcessingState.Ready);
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        SetupEntityLinkCreation();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse();
        result.Status.Should().Be("ready");
        result.PdfDocumentId.Should().Be(TestPdfId);
        result.Message.Should().Contain("già disponibile");

        _mediator.Verify(m => m.Send(
            It.Is<CreateEntityLinkCommand>(cmd =>
                cmd.SourceEntityType == MeepleEntityType.Game &&
                cmd.SourceEntityId == TestGameId &&
                cmd.TargetEntityType == MeepleEntityType.KbCard &&
                cmd.TargetEntityId == TestPdfId &&
                cmd.LinkType == EntityLinkType.RelatedTo &&
                cmd.Scope == EntityLinkScope.User),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Hash Match — Processing States

    [Fact]
    public async Task Handle_WhenHashMatchProcessing_ReturnsProcessingStatus()
    {
        // Arrange — Extracting is a mid-pipeline "processing" state
        var existingDoc = CreatePdfDocument(PdfProcessingState.Extracting);
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        SetupEntityLinkCreation();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse();
        result.Status.Should().Be("processing");
        result.PdfDocumentId.Should().Be(TestPdfId);
        result.Message.Should().Contain("in elaborazione");
    }

    [Fact]
    public async Task Handle_WhenHashMatchPending_ReturnsPendingStatus()
    {
        // Arrange — Pending is the initial pre-processing state
        var existingDoc = CreatePdfDocument(PdfProcessingState.Pending);
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        SetupEntityLinkCreation();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse();
        result.Status.Should().Be("pending");
        result.PdfDocumentId.Should().Be(TestPdfId);
        result.Message.Should().Contain("in elaborazione");
    }

    #endregion

    #region Hash Match — Failed State (Treats as New)

    [Fact]
    public async Task Handle_WhenHashMatchFailed_TreatsAsNewUpload()
    {
        // Arrange — Failed PDF should fall through to new upload
        var failedDoc = CreatePdfDocument(PdfProcessingState.Failed);
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedDoc);

        SetupNewUploadDependencies();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsNew.Should().BeTrue();
        result.Status.Should().Be("pending");
        result.Message.Should().Contain("caricato con successo");

        // Verify blob storage was called (new upload path)
        _blobStorageService.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region No Hash Match (New Upload)

    [Fact]
    public async Task Handle_WhenNoHashMatch_UploadsNewPdf()
    {
        // Arrange — no existing document
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        SetupNewUploadDependencies();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsNew.Should().BeTrue();
        result.Status.Should().Be("pending");
        result.Message.Should().Contain("caricato con successo");

        // Verify tier enforcement was checked
        _tierEnforcementService.Verify(t => t.CanPerformAsync(
            TestUserId, TierAction.UploadPdf, It.IsAny<CancellationToken>()), Times.Once);

        // Verify blob storage was used
        _blobStorageService.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify quota reservation
        _quotaService.Verify(q => q.ReserveQuotaAsync(
            TestUserId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify EntityLink creation
        _mediator.Verify(m => m.Send(
            It.IsAny<CreateEntityLinkCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DuplicateEntityLink Idempotency

    [Fact]
    public async Task Handle_WhenDuplicateEntityLink_IsIdempotent()
    {
        // Arrange — existing Ready PDF, but EntityLink already exists
        var existingDoc = CreatePdfDocument(PdfProcessingState.Ready);
        _pdfDocumentRepository
            .Setup(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        _mediator
            .Setup(m => m.Send(It.IsAny<CreateEntityLinkCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateEntityLinkException(
                MeepleEntityType.Game, TestGameId,
                MeepleEntityType.KbCard, TestPdfId,
                EntityLinkType.RelatedTo));

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — should succeed despite the duplicate exception
        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse();
        result.Status.Should().Be("ready");
        result.PdfDocumentId.Should().Be(TestPdfId);
    }

    #endregion

    #region Helpers

    private static PdfDocument CreatePdfDocument(PdfProcessingState state)
    {
        return PdfDocument.Reconstitute(
            id: TestPdfId,
            gameId: Guid.NewGuid(),
            fileName: new FileName("test-rulebook.pdf"),
            filePath: "/uploads/test-rulebook.pdf",
            fileSize: new FileSize(1024 * 1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow.AddHours(-1),
            processedAt: state == PdfProcessingState.Ready ? DateTime.UtcNow : null,
            pageCount: state == PdfProcessingState.Ready ? 20 : null,
            processingError: state == PdfProcessingState.Failed ? "Previous processing error" : null,
            language: new Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.LanguageCode("en"),
            processingState: state,
            contentHash: "abc123hash");
    }

    private AddRulebookCommand CreateCommand()
    {
        var fileMock = CreateMockPdfFile();
        return new AddRulebookCommand(TestGameId, TestUserId, fileMock.Object);
    }

    private static Mock<IFormFile> CreateMockPdfFile()
    {
        // Create a minimal valid PDF-like content
        var pdfContent = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nxref\n0 1\ntrailer\n<< >>\nstartxref\n0\n%%EOF"u8.ToArray();
        var stream = new MemoryStream(pdfContent);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfContent));
        fileMock.Setup(f => f.Length).Returns(pdfContent.Length);
        fileMock.Setup(f => f.FileName).Returns("test-rulebook.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");

        return fileMock;
    }

    private void SetupEntityLinkCreation()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateEntityLinkCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityLinkDto(
                Id: Guid.NewGuid(),
                SourceEntityType: MeepleEntityType.Game,
                SourceEntityId: TestGameId,
                TargetEntityType: MeepleEntityType.KbCard,
                TargetEntityId: TestPdfId,
                LinkType: EntityLinkType.RelatedTo,
                IsBidirectional: true,
                Scope: EntityLinkScope.User,
                OwnerUserId: TestUserId,
                Metadata: null,
                IsAdminApproved: true,
                IsBggImported: false,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow));
    }

    private void SetupNewUploadDependencies()
    {
        // Tier enforcement allows upload
        _tierEnforcementService
            .Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), TierAction.UploadPdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tierEnforcementService
            .Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.Unlimited);

        // Blob storage succeeds
        var fileId = Guid.NewGuid().ToString("N");
        _blobStorageService
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, fileId, "/uploads/test.pdf", 1024));

        // Quota reservation succeeds
        _quotaService
            .Setup(q => q.ReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaReservationResult.Success(DateTime.UtcNow.AddHours(1)));

        // EntityLink creation succeeds
        SetupEntityLinkCreation();

        // Enqueue succeeds (via MediatR)
        _mediator
            .Setup(m => m.Send(It.IsAny<Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue.EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    #endregion
}
