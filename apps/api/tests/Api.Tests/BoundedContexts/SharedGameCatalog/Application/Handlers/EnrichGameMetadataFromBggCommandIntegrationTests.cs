using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Integration tests for EnrichGameMetadataFromBggCommandHandler.
/// Tests real BGG API client integration with mocked HTTP responses.
/// Issue #4156: BGG Match and Enrichment Command
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class EnrichGameMetadataFromBggCommandIntegrationTests
{
    private readonly Mock<IBggApiClient> _bggApiClientMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly EnrichGameMetadataFromBggCommandHandler _handler;

    public EnrichGameMetadataFromBggCommandIntegrationTests()
    {
        _bggApiClientMock = new Mock<IBggApiClient>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_bggApiClientMock.Object);
        services.AddSingleton<EnrichGameMetadataFromBggCommandHandler>();

        // Add HybridCache (required by some BGG client implementations)
        services.AddHybridCache();

        _serviceProvider = services.BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EnrichGameMetadataFromBggCommandHandler>();
    }

    #region End-to-End Integration Tests

    [Fact]
    public async Task Handle_EndToEnd_WithSuccessfulBggEnrichment()
    {
        // Arrange - Simulate real wizard workflow
        var userId = Guid.NewGuid();

        // Step 1: PDF extraction result (from issue #4155)
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Wingspan",
            Year = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTime = 40,
            MinAge = 10,
            Description = "Bird-themed engine building game",
            ConfidenceScore = 0.92
        };

        // Step 2: Mock BGG API response
        var bggData = new BggGameDetails
        {
            BggId = 266192,
            Title = "Wingspan",
            YearPublished = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 70, // Conflict with PDF
            Description = "Official BGG description with more details",
            ComplexityRating = 2.42m,
            ImageUrl = "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/VKrRT2ui1a6FEFCoouRqVRQdodY=/0x0/filters:format(jpeg)/pic4458123.jpg",
            ThumbnailUrl = "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__thumb/img/r6tIEYPzpFnVk7W0nJ_j3GXKqKE=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg",
            AverageRating = 8.0m,
            RankPosition = 12
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(266192, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 266192, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify enriched result
        result.Should().NotBeNull();
        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.EnrichmentWarning.Should().BeNull();

        // Merged fields
        result.Title.Should().Be("Wingspan"); // BGG title matches
        result.Year.Should().Be(2019);
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(5);
        result.PlayingTime.Should().Be(70); // BGG preferred
        result.MinAge.Should().Be(10); // PDF only (BGG doesn't provide)
        result.Description.Should().Contain("Official BGG description"); // BGG preferred

        // BGG-only fields
        result.ComplexityRating.Should().Be(2.42m);
        result.ImageUrl.Should().NotBeNullOrEmpty();
        result.ThumbnailUrl.Should().NotBeNullOrEmpty();
        result.AverageRating.Should().Be(8.0m);
        result.RankPosition.Should().Be(12);

        // Metadata
        result.BggId.Should().Be(266192);
        result.PdfConfidenceScore.Should().Be(0.92);

        // Conflicts
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].FieldName.Should().Be("PlayingTime");
        result.Conflicts[0].BggValue.Should().Be("70");
        result.Conflicts[0].PdfValue.Should().Be("40");
    }

    [Fact]
    public async Task Handle_EndToEnd_WithBggEnrichmentFailure_PreservesPdfData()
    {
        // Arrange - Simulate BGG API down scenario
        var userId = Guid.NewGuid();

        var pdfMetadata = new GameMetadataDto
        {
            Title = "Obscure Indie Game",
            Year = 2023,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 45,
            MinAge = 14,
            Description = "Unique PDF-extracted description",
            ConfidenceScore = 0.68
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(999999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("BGG API 503 Service Unavailable"));

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 999999, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify graceful fallback
        result.Should().NotBeNull();
        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("failed");

        // All PDF data preserved
        result.Title.Should().Be("Obscure Indie Game");
        result.Year.Should().Be(2023);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTime.Should().Be(45);
        result.MinAge.Should().Be(14);
        result.Description.Should().Be("Unique PDF-extracted description");
        result.PdfConfidenceScore.Should().Be(0.68);

        // BGG-only fields null
        result.ComplexityRating.Should().BeNull();
        result.ImageUrl.Should().BeNull();
        result.AverageRating.Should().BeNull();

        // No conflicts (BGG data unavailable)
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithRealWorldConflictScenario_DetectsMultipleConflicts()
    {
        // Arrange - Simulate common conflict scenario (different box editions)
        var userId = Guid.NewGuid();

        // PDF extracted from 2nd edition rulebook
        var pdfMetadata = new GameMetadataDto
        {
            Title = "Ticket to Ride",
            Year = 2021, // 2nd edition year
            MinPlayers = 2,
            MaxPlayers = 5,
            PlayingTime = 45,
            MinAge = 8,
            ConfidenceScore = 0.88
        };

        // BGG data for 1st edition
        var bggData = new BggGameDetails
        {
            BggId = 9209,
            Title = "Ticket to Ride",
            YearPublished = 2004, // Original edition
            MinPlayers = 2,
            MaxPlayers = 5,
            PlayingTimeMinutes = 60, // Different timing
            ComplexityRating = 1.89m,
            AverageRating = 7.4m,
            RankPosition = 50
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(9209, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 9209, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify conflict detection for edition differences
        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.Conflicts.Should().HaveCount(2);

        result.Conflicts.Should().Contain(c =>
            c.FieldName == "Year" && c.BggValue == "2004" && c.PdfValue == "2021");
        result.Conflicts.Should().Contain(c =>
            c.FieldName == "PlayingTime" && c.BggValue == "60" && c.PdfValue == "45");

        // BGG values preferred in final result
        result.Year.Should().Be(2004);
        result.PlayingTime.Should().Be(60);
    }

    [Fact]
    public async Task Handle_WithLowConfidencePdfData_StillEnrichesSuccessfully()
    {
        // Arrange - Low quality PDF extraction, high quality BGG data
        var userId = Guid.NewGuid();

        var pdfMetadata = new GameMetadataDto
        {
            Title = "Azul", // Correct title
            Year = null, // Missing
            MinPlayers = null, // Missing
            MaxPlayers = null, // Missing
            PlayingTime = 30, // Partially extracted
            MinAge = null, // Missing
            Description = null, // Missing
            ConfidenceScore = 0.35 // Low confidence
        };

        var bggData = new BggGameDetails
        {
            BggId = 230802,
            Title = "Azul",
            YearPublished = 2017,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            Description = "Tile-drafting game inspired by Portuguese tiles",
            ComplexityRating = 1.78m,
            ImageUrl = "https://example.com/azul.jpg",
            AverageRating = 7.8m,
            RankPosition = 25
        };

        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(230802, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggData);

        var command = new EnrichGameMetadataFromBggCommand(pdfMetadata, 230802, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - BGG fills in missing data
        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.Title.Should().Be("Azul");
        result.Year.Should().Be(2017); // BGG filled
        result.MinPlayers.Should().Be(2); // BGG filled
        result.MaxPlayers.Should().Be(4); // BGG filled
        result.PlayingTime.Should().Be(45); // BGG value (conflict)
        result.Description.Should().NotBeNullOrEmpty(); // BGG filled
        result.PdfConfidenceScore.Should().Be(0.35);

        // Only one conflict (PlayingTime)
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].FieldName.Should().Be("PlayingTime");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PropagatesCancellation()
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

    #endregion
}
