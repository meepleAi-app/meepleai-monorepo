using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for GetGamePdfsQueryHandler.
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGamePdfsQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockRepository;
    private readonly Mock<ILogger<GetGamePdfsQueryHandler>> _mockLogger;
    private readonly GetGamePdfsQueryHandler _handler;

    public GetGamePdfsQueryHandlerTests()
    {
        _mockRepository = new Mock<IUserLibraryRepository>();
        _mockLogger = new Mock<ILogger<GetGamePdfsQueryHandler>>();
        _handler = new GetGamePdfsQueryHandler(
            _mockRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WhenGameNotInLibrary_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _mockRepository.Verify(
            r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WhenGameHasNoCustomPdf_ReturnsMockStandardPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Source.Should().Be("Catalog");
        result[0].Name.Should().Be("Regolamento Standard");
        result[0].Language.Should().Be("IT");
    }

    [Fact]
    public async Task Handle_WhenGameHasCustomPdf_ReturnsCustomPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var customPdf = CustomPdfMetadata.Create(
            url: "https://example.com/my-custom-rules-IT.pdf",
            fileSizeBytes: 2_500_000,
            originalFileName: "CustomRules-IT.pdf"
        );
        libraryEntry.UploadCustomPdf(customPdf);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Source.Should().Be("Custom");
        result[0].Name.Should().Be("CustomRules-IT");
        result[0].FileSizeBytes.Should().Be(2_500_000);
        result[0].Language.Should().Be("IT");
    }

    [Fact]
    public async Task Handle_DetectsItalianLanguageFromFilename()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var customPdf = CustomPdfMetadata.Create(
            url: "https://example.com/rules.pdf",
            fileSizeBytes: 1_000_000,
            originalFileName: "Regolamento_it.pdf"
        );
        libraryEntry.UploadCustomPdf(customPdf);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].Language.Should().Be("IT");
    }

    [Fact]
    public async Task Handle_DetectsEnglishLanguageFromFilename()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var customPdf = CustomPdfMetadata.Create(
            url: "https://example.com/rules.pdf",
            fileSizeBytes: 1_000_000,
            originalFileName: "Rulebook-EN.pdf"
        );
        libraryEntry.UploadCustomPdf(customPdf);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].Language.Should().Be("EN");
    }

    [Fact]
    public async Task Handle_ReturnsNullLanguageWhenUndetectable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var customPdf = CustomPdfMetadata.Create(
            url: "https://example.com/rules.pdf",
            fileSizeBytes: 1_000_000,
            originalFileName: "MyRules.pdf" // No language indicator
        );
        libraryEntry.UploadCustomPdf(customPdf);

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result[0].Language.Should().BeNull();
    }
}
