using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for EnrichGameMetadataFromBggCommandHandler.
/// Tests BGG enrichment, metadata merging, and conflict detection.
/// Issue #4156: BGG Match and Enrichment Command
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class EnrichGameMetadataFromBggCommandHandlerTests
{
    private readonly Mock<IBggApiClient> _bggApiClientMock;
    private readonly Mock<ILogger<EnrichGameMetadataFromBggCommandHandler>> _loggerMock;
    private readonly EnrichGameMetadataFromBggCommandHandler _handler;

    public EnrichGameMetadataFromBggCommandHandlerTests()
    {
        _bggApiClientMock = new Mock<IBggApiClient>();
        _loggerMock = new Mock<ILogger<EnrichGameMetadataFromBggCommandHandler>>();
        _handler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            _loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBggApiClient_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new EnrichGameMetadataFromBggCommandHandler(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bggApiClient");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new EnrichGameMetadataFromBggCommandHandler(_bggApiClientMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle - Successful Enrichment

    [Fact]
    public async Task Handle_WithBothPdfAndBggData_MergesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Catan",
            Year = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTime = 90,
            MinAge = 10,
            Description = "PDF description",
            ConfidenceScore = 0.85
        };

        var bggData = new BggGameDetails
        {
            BggId = 13,
            Title = "The Settlers of Catan",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 120,
            Description = "BGG description",
            ComplexityRating = 2.34m,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            AverageRating = 7.5m,
            RankPosition = 100
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 13, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("The Settlers of Catan"); // BGG preferred
        result.Year.Should().Be(1995); // Match, no conflict
        result.MinPlayers.Should().Be(3); // Match, no conflict
        result.MaxPlayers.Should().Be(4); // Match, no conflict
        result.PlayingTime.Should().Be(120); // BGG preferred (conflict)
        result.MinAge.Should().Be(10); // PDF only
        result.Description.Should().Be("BGG description"); // BGG preferred
        result.ComplexityRating.Should().Be(2.34m);
        result.ImageUrl.Should().Be("https://example.com/image.jpg");
        result.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        result.AverageRating.Should().Be(7.5m);
        result.RankPosition.Should().Be(100);
        result.BggId.Should().Be(13);
        result.PdfConfidenceScore.Should().Be(0.85);
        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.EnrichmentWarning.Should().BeNull();

        // Verify conflict detection
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].FieldName.Should().Be("PlayingTime");
        result.Conflicts[0].BggValue.Should().Be("120");
        result.Conflicts[0].PdfValue.Should().Be("90");
    }

    [Fact]
    public async Task Handle_WithOnlyPdfData_UsesPdfDataSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Custom Game",
            Year = 2024,
            MinPlayers = 2,
            MaxPlayers = 6,
            PlayingTime = 60,
            MinAge = 12,
            Description = "Custom description",
            ConfidenceScore = 0.75
        };

        var bggData = new BggGameDetails
        {
            BggId = 999,
            Title = "BGG Game Title",
            YearPublished = null,
            MinPlayers = null,
            MaxPlayers = null,
            PlayingTimeMinutes = null,
            Description = null
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 999, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("BGG Game Title"); // BGG always preferred for title
        result.Year.Should().Be(2024); // PDF only
        result.MinPlayers.Should().Be(2); // PDF only
        result.MaxPlayers.Should().Be(6); // PDF only
        result.PlayingTime.Should().Be(60); // PDF only
        result.MinAge.Should().Be(12); // PDF only
        result.Description.Should().Be("Custom description"); // PDF fallback
        result.Conflicts.Should().BeEmpty(); // No conflicts when only one source has data
        result.BggEnrichmentSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithOnlyBggData_UsesBggDataSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = null,
            Year = null,
            MinPlayers = null,
            MaxPlayers = null,
            PlayingTime = null,
            MinAge = null,
            Description = null,
            ConfidenceScore = 0.2
        };

        var bggData = new BggGameDetails
        {
            BggId = 555,
            Title = "Pure BGG Game",
            YearPublished = 2023,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 45,
            Description = "BGG only description"
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(555, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 555, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("Pure BGG Game");
        result.Year.Should().Be(2023); // BGG only
        result.MinPlayers.Should().Be(1); // BGG only
        result.MaxPlayers.Should().Be(5); // BGG only
        result.PlayingTime.Should().Be(45); // BGG only
        result.MinAge.Should().BeNull(); // Neither source
        result.Description.Should().Be("BGG only description");
        result.Conflicts.Should().BeEmpty();
        result.BggEnrichmentSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithMultipleConflicts_DetectsAllConflicts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Game",
            Year = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 60,
            MinAge = 8,
            ConfidenceScore = 0.9
        };

        var bggData = new BggGameDetails
        {
            BggId = 777,
            Title = "Game",
            YearPublished = 2021, // Conflict
            MinPlayers = 3, // Conflict
            MaxPlayers = 6, // Conflict
            PlayingTimeMinutes = 90 // Conflict
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(777, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 777, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Conflicts.Should().HaveCount(4);
        result.Conflicts.Should().Contain(c => c.FieldName == "Year" && c.BggValue == "2021" && c.PdfValue == "2020");
        result.Conflicts.Should().Contain(c => c.FieldName == "MinPlayers" && c.BggValue == "3" && c.PdfValue == "2");
        result.Conflicts.Should().Contain(c => c.FieldName == "MaxPlayers" && c.BggValue == "6" && c.PdfValue == "4");
        result.Conflicts.Should().Contain(c => c.FieldName == "PlayingTime" && c.BggValue == "90" && c.PdfValue == "60");

        // Verify BGG values are preferred
        result.Year.Should().Be(2021);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(6);
        result.PlayingTime.Should().Be(90);
    }

    #endregion

    #region Handle - Graceful Fallback Scenarios

    [Fact]
    public async Task Handle_WhenBggApiTimesOut_FallsBackToPdfData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Offline Game",
            Year = 2022,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 30,
            MinAge = 8,
            Description = "PDF description only",
            ConfidenceScore = 0.8
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(123, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("BGG API timeout"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 123, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Offline Game");
        result.Year.Should().Be(2022);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTime.Should().Be(30);
        result.Description.Should().Be("PDF description only");
        result.ComplexityRating.Should().BeNull();
        result.ImageUrl.Should().BeNull();
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("timed out");
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenBggApiThrowsHttpException_FallsBackToPdfData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Network Error Game",
            Year = 2021,
            PlayingTime = 45,
            ConfidenceScore = 0.7
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(456, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404 Not Found"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 456, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("Network Error Game");
        result.Year.Should().Be(2021);
        result.PlayingTime.Should().Be(45);
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("failed");
    }

    [Fact]
    public async Task Handle_WhenBggApiThrowsUnexpectedException_FallsBackToPdfData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Exception Game",
            MinPlayers = 1,
            MaxPlayers = 8,
            ConfidenceScore = 0.6
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(789, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 789, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("Exception Game");
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(8);
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task Handle_WithEmptyPdfMetadataAndBggFailure_ReturnsUnknownGameTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = GameMetadataDto.CreateEmpty();

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("BGG down"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 999, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("Unknown Game"); // Default fallback
        result.Year.Should().BeNull();
        result.MinPlayers.Should().BeNull();
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.PdfConfidenceScore.Should().Be(0.0);
    }

    #endregion

    #region Handle - Edge Cases

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithCancellationRequested_PropagatesCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Test",
            ConfidenceScore = 0.5
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 1, userId);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_WithZeroBggId_StillProcesses()
    {
        // Arrange (edge case: BGG ID 0 is invalid but should not crash)
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Test Game",
            Year = 2023,
            ConfidenceScore = 0.8
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Invalid BGG ID"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 0, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BggId.Should().Be(0);
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.Title.Should().Be("Test Game");
    }

    [Fact]
    public async Task Handle_WithMatchingData_NoConflicts()
    {
        // Arrange (perfect match between PDF and BGG)
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Perfect Match",
            Year = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 60,
            ConfidenceScore = 1.0
        };

        var bggData = new BggGameDetails
        {
            BggId = 111,
            Title = "Perfect Match",
            YearPublished = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(111, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 111, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Conflicts.Should().BeEmpty(); // Perfect match = no conflicts
        result.BggEnrichmentSucceeded.Should().BeTrue();
    }

    #endregion

    #region Logging Verification

    [Fact]
    public async Task Handle_OnSuccessfulEnrichment_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Test",
            ConfidenceScore = 0.8
        };

        var bggData = new BggGameDetails
        {
            BggId = 100,
            Title = "Test Game"
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 100, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully fetched BGG data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnBggTimeout_LogsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Test",
            ConfidenceScore = 0.8
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(200, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 200, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("BGG API timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnUnexpectedException_LogsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Test",
            ConfidenceScore = 0.8
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(300, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 300, userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Unexpected error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
