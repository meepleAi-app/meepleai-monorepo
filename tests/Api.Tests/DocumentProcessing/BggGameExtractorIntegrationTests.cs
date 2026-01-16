using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Api.Tests.DocumentProcessing;

[Collection("SharedTestcontainers")]
public sealed class BggGameExtractorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly IMediator _mediator;
    private readonly string _testPdfPath;

    public BggGameExtractorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _mediator = _fixture.CreateMediator();

        // Path to real test PDF
        _testPdfPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", // Navigate to repo root
            "data", "pdfDocs", "Top 100 giochi BGG.pdf");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExtractBggGamesFromPdfQuery_WithRealPdf_ShouldExtractGames()
    {
        // Skip if PDF not found (CI environment may not have test data)
        if (!File.Exists(_testPdfPath))
        {
            // Use Skip instead of throwing to allow CI to pass
            return;
        }

        // Arrange
        var query = new ExtractBggGamesFromPdfQuery(_testPdfPath);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThan(100); // PDF contains ~180 games

        // Validate known games from PDF
        result.Should().Contain(g => g.GameName == "Brass: Birmingham" && g.BggId == 224517);
        result.Should().Contain(g => g.GameName == "Gloomhaven" && g.BggId == 174430);
        result.Should().Contain(g => g.GameName == "Wingspan" && g.BggId == 266192);
        result.Should().Contain(g => g.GameName == "Codenames" && g.BggId == 178900);
        result.Should().Contain(g => g.GameName == "Terraforming Mars" && g.BggId == 167791);

        // Validate all entries meet validation rules
        result.Should().AllSatisfy(game =>
        {
            game.GameName.Should().NotBeNullOrWhiteSpace();
            game.GameName.Length.Should().BeGreaterOrEqualTo(2);
            game.BggId.Should().BeGreaterThan(0);
        });

        // Check for duplicate IDs (should be unique)
        var uniqueIds = result.Select(g => g.BggId).Distinct().Count();
        uniqueIds.Should().Be(result.Count, "all BGG IDs should be unique");
    }

    [Fact]
    public async Task ExtractBggGamesFromPdfQuery_WithNonExistentPdf_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.pdf");
        var query = new ExtractBggGamesFromPdfQuery(nonExistentPath);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _mediator.Send(query));
    }

    [Fact]
    public async Task ExtractBggGamesFromPdfQuery_WithValidPdf_ShouldExtractSpecialCharacterNames()
    {
        // Skip if PDF not found
        if (!File.Exists(_testPdfPath))
        {
            return;
        }

        // Arrange
        var query = new ExtractBggGamesFromPdfQuery(_testPdfPath);

        // Act
        var result = await _mediator.Send(query);

        // Assert - Games with special characters
        result.Should().Contain(g => g.GameName == "The Lord of the Rings: The Card Game" && g.BggId == 421006);
        result.Should().Contain(g => g.GameName == "7 Wonders Duel" && g.BggId == 173346);
        result.Should().Contain(g => g.GameName == "Clank!: A Deck-Building Adventure" && g.BggId == 201808);
        result.Should().Contain(g => g.GameName == "Dune: Imperium – Uprising" && g.BggId == 397598);
    }

    [Fact]
    public async Task ExtractBggGamesFromPdfQuery_WithValidPdf_ShouldExtractBothLists()
    {
        // Skip if PDF not found
        if (!File.Exists(_testPdfPath))
        {
            return;
        }

        // Arrange
        var query = new ExtractBggGamesFromPdfQuery(_testPdfPath);

        // Act
        var result = await _mediator.Send(query);

        // Assert - Games from "Currently Popular" list (page 1)
        result.Should().Contain(g => g.GameName == "Codenames" && g.BggId == 178900);
        result.Should().Contain(g => g.GameName == "Ticket to Ride" && g.BggId == 9209);
        result.Should().Contain(g => g.GameName == "Pandemic" && g.BggId == 30549);

        // Assert - Games from "Historically Popular" list (page 2-4)
        result.Should().Contain(g => g.GameName == "Brass: Birmingham" && g.BggId == 224517);
        result.Should().Contain(g => g.GameName == "Twilight Imperium: Fourth Edition" && g.BggId == 233078);
        result.Should().Contain(g => g.GameName == "Frosthaven" && g.BggId == 295770);
    }
}
