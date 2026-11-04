using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using PdfFileName = Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.FileName;
using PdfFileSize = Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.FileSize;
using PdfLanguageCode = Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.LanguageCode;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for CreateSharedGameFromPdfCommandHandler (Issue #4140).
/// Tests the 8-step workflow: fetch PDF, duplicate check, defaults, BGG merge,
/// create game, link PDF, approval workflow, return result.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4140")]
public sealed class CreateSharedGameFromPdfCommandHandlerTests : IDisposable
{
    private readonly Mock<IPdfDocumentRepository> _pdfRepositoryMock;
    private readonly Mock<ISharedGameRepository> _gameRepositoryMock;
    private readonly Mock<IBggApiService> _bggApiServiceMock;
    private readonly Mock<IBggCoverDownloader> _bggCoverDownloaderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<CreateSharedGameFromPdfCommandHandler>> _loggerMock;
    private readonly CreateSharedGameFromPdfCommandHandler _handler;

    public CreateSharedGameFromPdfCommandHandlerTests()
    {
        _pdfRepositoryMock = new Mock<IPdfDocumentRepository>();
        _gameRepositoryMock = new Mock<ISharedGameRepository>();
        _bggApiServiceMock = new Mock<IBggApiService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        // Issue #3866: the unit of work is a mock, so the handler's final SaveChangesAsync wrote
        // nothing — the persistence assertions passed only because a tracking-by-default test
        // context resolved the reload to the same in-memory instance the handler had mutated.
        // Wiring the mock to the real SaveChangesAsync makes them verify what they claim to.
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _dbContext.SaveChangesAsync(ct));
        _loggerMock = new Mock<ILogger<CreateSharedGameFromPdfCommandHandler>>();

        // Default: downloader returns null (no-op fallback) for all calls.
        // Tests that verify cover upload success set up explicit returns.
        _bggCoverDownloaderMock = new Mock<IBggCoverDownloader>();
        _bggCoverDownloaderMock
            .Setup(d => d.DownloadAndUploadAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // InMemory database for duplicate check query (uses _dbContext.Set<SharedGameEntity>())
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"PdfWizardTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new CreateSharedGameFromPdfCommandHandler(
            _pdfRepositoryMock.Object,
            _gameRepositoryMock.Object,
            _bggApiServiceMock.Object,
            _bggCoverDownloaderMock.Object,
            _unitOfWorkMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static CreateSharedGameFromPdfCommand CreateValidCommand(
        Guid? pdfDocumentId = null,
        Guid? userId = null,
        string title = "Catan",
        int? minPlayers = null,
        int? maxPlayers = null,
        int? playingTimeMinutes = null,
        int? minAge = null,
        int? selectedBggId = null,
        bool requiresApproval = false)
        => new(
            PdfDocumentId: pdfDocumentId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            ExtractedTitle: title,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            PlayingTimeMinutes: playingTimeMinutes,
            MinAge: minAge,
            SelectedBggId: selectedBggId,
            RequiresApproval: requiresApproval);

    private static PdfDocument CreateMockPdfDocument(Guid id)
    {
        return PdfDocument.Reconstitute(
            id: id,
            gameId: Guid.NewGuid(),
            fileName: new PdfFileName("test.pdf"),
            filePath: "/uploads/test.pdf",
            fileSize: new PdfFileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow.AddMinutes(-10),
            processedAt: DateTime.UtcNow.AddMinutes(-5),
            pageCount: 10,
            processingError: null,
            language: PdfLanguageCode.English);
    }

    private static BggGameDetailsDto CreateBggDetails(
        int bggId = 13,
        string name = "Catan",
        string description = "A strategy game of trading and building",
        int yearPublished = 1995,
        int minPlayers = 3,
        int maxPlayers = 4,
        int playingTime = 90,
        int minAge = 10,
        double averageRating = 7.2,
        double averageWeight = 2.3,
        string? imageUrl = "https://example.com/img.jpg",
        string? thumbnailUrl = "https://example.com/thumb.jpg")
        => new(
            BggId: bggId,
            Name: name,
            Description: description,
            YearPublished: yearPublished,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            PlayingTime: playingTime,
            MinPlayTime: null,
            MaxPlayTime: null,
            MinAge: minAge,
            AverageRating: averageRating,
            BayesAverageRating: null,
            UsersRated: null,
            AverageWeight: averageWeight,
            ThumbnailUrl: thumbnailUrl,
            ImageUrl: imageUrl,
            Categories: new List<string>(),
            Mechanics: new List<string>(),
            Designers: new List<string>(),
            Publishers: new List<string>());

    private void SetupPdfFound(Guid pdfId)
    {
        _pdfRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockPdfDocument(pdfId));
    }

    private void SetupBggDetails(int bggId, BggGameDetailsDto? details)
    {
        _bggApiServiceMock
            .Setup(b => b.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
    }

    #region Success Scenarios with BGG Enrichment

    [Fact]
    public async Task Handle_WithBggEnrichment_CreatesGameSuccessfully()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 174430;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Gloomhaven", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(
            bggId: bggId, name: "Gloomhaven", description: "A tactical combat game",
            yearPublished: 2017, minPlayers: 1, maxPlayers: 4, playingTime: 120,
            minAge: 14, averageRating: 8.5, averageWeight: 3.87,
            imageUrl: "https://example.com/gloom.jpg", thumbnailUrl: "https://example.com/gloom-thumb.jpg"));

        SharedGame? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().NotBe(Guid.Empty);
        result.ApprovalStatus.Should().Be("Published");
        result.BggEnrichmentApplied.Should().BeTrue();
        result.EnrichedWithBggId.Should().Be(bggId);

        capturedGame.Should().NotBeNull();
        capturedGame!.Title.Should().Be("Gloomhaven");
        capturedGame.MinPlayers.Should().Be(1);
        capturedGame.MaxPlayers.Should().Be(4);
        capturedGame.PlayingTimeMinutes.Should().Be(120);
        capturedGame.MinAge.Should().Be(14);
        capturedGame.YearPublished.Should().Be(2017);
        capturedGame.ImageUrl.Should().Be("https://example.com/gloom.jpg");
        capturedGame.ThumbnailUrl.Should().Be("https://example.com/gloom-thumb.jpg");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithManualOverridesAndBgg_ManualOverridesTakePrecedence()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 167791;
        var command = CreateValidCommand(
            pdfDocumentId: pdfId, title: "Terraforming Mars",
            minPlayers: 2, maxPlayers: 6, playingTimeMinutes: 180, minAge: 14,
            selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(
            bggId: bggId, name: "Terraforming Mars", description: "Compete to transform Mars",
            yearPublished: 2016, minPlayers: 1, maxPlayers: 5, playingTime: 120, minAge: 12));

        SharedGame? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - manual overrides preserved over BGG data
        capturedGame.Should().NotBeNull();
        capturedGame!.MinPlayers.Should().Be(2); // manual override, NOT BGG's 1
        capturedGame.MaxPlayers.Should().Be(6); // manual override, NOT BGG's 5
        capturedGame.PlayingTimeMinutes.Should().Be(180); // manual override, NOT BGG's 120
        capturedGame.MinAge.Should().Be(14); // manual override, NOT BGG's 12
        result.BggEnrichmentApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithPartialManualOverrides_MixesBggAndManual()
    {
        // Arrange - only override minPlayers, let BGG fill the rest
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(
            pdfDocumentId: pdfId, title: "Catan",
            minPlayers: 2, // manual override
            selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(
            bggId: bggId, minPlayers: 3, maxPlayers: 4, playingTime: 90, minAge: 10));

        SharedGame? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedGame.Should().NotBeNull();
        capturedGame!.MinPlayers.Should().Be(2); // manual override
        capturedGame.MaxPlayers.Should().Be(4); // from BGG
        capturedGame.PlayingTimeMinutes.Should().Be(90); // from BGG
        capturedGame.MinAge.Should().Be(10); // from BGG
    }

    #endregion

    #region BGG Failure Handling

    [Fact]
    public async Task Handle_WithBggReturnsNull_FailsDueToMissingDescription()
    {
        // Arrange - BGG returns null, handler uses empty description which fails SharedGame.Create validation
        var pdfId = Guid.NewGuid();
        var bggId = 999999;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Unknown Game", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, null);

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - handler passes empty description to SharedGame.Create() which requires non-empty
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*description*");
    }

    [Fact]
    public async Task Handle_WithoutBggId_FailsDueToMissingDescription()
    {
        // Arrange - no BGG enrichment means empty description
        var pdfId = Guid.NewGuid();
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "No BGG Game");

        SetupPdfFound(pdfId);

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - SharedGame.Create requires non-empty description
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*description*");
    }

    #endregion

    #region Approval Workflow

    [Fact]
    public async Task Handle_WithRequiresApproval_SetsStatusToDraft()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(
            pdfDocumentId: pdfId, title: "Draft Game",
            selectedBggId: bggId, requiresApproval: true);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails());

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovalStatus.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_WithoutRequiresApproval_SetsStatusToPublished()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(
            pdfDocumentId: pdfId, title: "Published Game",
            selectedBggId: bggId, requiresApproval: false);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails());

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovalStatus.Should().Be("Published");
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Handle_WithPdfNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Missing PDF Game");

        _pdfRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{pdfId}*");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Repository Interactions

    [Fact]
    public async Task Handle_Success_CallsSaveChangesOnce()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "SaveTest Game", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails());

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithBggId_CallsBggApiService()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Catan", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails());

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _bggApiServiceMock.Verify(b => b.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutBggId_DoesNotCallBggApiService()
    {
        // Arrange - no BGG, will fail on description validation but BGG API should not be called
        var pdfId = Guid.NewGuid();
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "No BGG Game");

        SetupPdfFound(pdfId);

        // Act - expect failure due to empty description, but verify BGG not called
        try
        {
            await _handler.Handle(command, CancellationToken.None);
        }
        catch (ArgumentException)
        {
            // Expected: SharedGame.Create fails with empty description
        }

        // Assert
        _bggApiServiceMock.Verify(
            b => b.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Result Mapping

    [Fact]
    public async Task Handle_WithBggEnrichment_MapsResultCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Catan", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(bggId: bggId));

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.GameId.Should().NotBe(Guid.Empty);
        result.QualityScore.Should().Be(0.85); // default quality score
        result.BggEnrichmentApplied.Should().BeTrue();
        result.EnrichedWithBggId.Should().Be(bggId);
        result.ApprovalStatus.Should().Be("Published");
        result.DuplicateWarning.Should().BeFalse(); // empty DB = no duplicates
        result.DuplicateTitles.Should().BeEmpty();
    }

    #endregion

    #region BGG Cover Re-upload (Gap G2)

    [Fact]
    public async Task Handle_WithBggIdAndCoverDownloadSuccess_SetsBggCoverR2KeyOnEntity()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var expectedR2Key = "bgg-cover-13";
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Catan", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(bggId: bggId, imageUrl: "https://cf.geekdo-images.com/abc.jpg"));

        // The mock AddAsync callback seeds the entity into InMemory DbContext so the handler
        // can find it when setting BggCoverR2Key via FirstOrDefaultAsync.
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns<SharedGame, CancellationToken>(async (g, ct) =>
            {
                var entity = new SharedGameEntity
                {
                    Id = g.Id,
                    Title = g.Title,
                    Description = g.Description,
                    YearPublished = g.YearPublished,
                    MinPlayers = g.MinPlayers,
                    MaxPlayers = g.MaxPlayers,
                    PlayingTimeMinutes = g.PlayingTimeMinutes,
                    MinAge = g.MinAge,
                    ImageUrl = g.ImageUrl,
                    ThumbnailUrl = g.ThumbnailUrl,
                    CreatedBy = g.CreatedBy,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.Set<SharedGameEntity>().AddAsync(entity, ct);
                await _dbContext.SaveChangesAsync(ct);
            });

        _bggCoverDownloaderMock
            .Setup(d => d.DownloadAndUploadAsync(bggId, "https://cf.geekdo-images.com/abc.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedR2Key);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.BggEnrichmentApplied.Should().BeTrue();

        // Verify BggCoverR2Key was set on the EF-tracked entity.
        // Note: _unitOfWork.SaveChangesAsync is mocked, but EF InMemory change tracking
        // reflects the modification in-memory. We look up the entity by its saved ID.
        var savedEntity = _dbContext.ChangeTracker.Entries<SharedGameEntity>()
            .FirstOrDefault(e => e.Entity.BggCoverR2Key == expectedR2Key)?.Entity;
        savedEntity.Should().NotBeNull("downloader should have set BggCoverR2Key on the tracked entity");
        savedEntity!.BggCoverR2Key.Should().Be(expectedR2Key);

        // Polish: also verify the value survives a full DbContext round-trip query
        var persistedEntity = await _dbContext.Set<SharedGameEntity>()
            .FirstAsync(e => e.Id == result.GameId);
        persistedEntity.BggCoverR2Key.Should().Be(expectedR2Key);

        // ImageUrl remains BGG direct URL (convention F4 — CoverUrlResolver is authoritative display source)
        savedEntity.ImageUrl.Should().Be("https://cf.geekdo-images.com/abc.jpg");
    }

    [Fact]
    public async Task Handle_WithBggIdAndCoverDownloadFailure_FallsBackToDirectUrl()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var bggId = 13;
        var command = CreateValidCommand(pdfDocumentId: pdfId, title: "Catan", selectedBggId: bggId);

        SetupPdfFound(pdfId);
        SetupBggDetails(bggId, CreateBggDetails(bggId: bggId, imageUrl: "https://cf.geekdo-images.com/abc.jpg"));

        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Downloader returns null (failure / already configured as default no-op)

        // Act — should not throw; fallback is silent
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.BggEnrichmentApplied.Should().BeTrue();
        result.GameId.Should().NotBe(Guid.Empty);
        _bggCoverDownloaderMock.Verify(
            d => d.DownloadAndUploadAsync(bggId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
