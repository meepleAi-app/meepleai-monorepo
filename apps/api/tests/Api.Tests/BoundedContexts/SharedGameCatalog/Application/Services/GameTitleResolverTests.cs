using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities; // SharedGameTranslation + GameStatus
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="GameTitleResolver"/> — the batch enricher that joins
/// non-EN translations onto <see cref="SharedGameDto"/> list-query results.
/// Issue #2339 — sub-PR 1/3 Wave 3 (Task 8).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GameTitleResolverTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISharedGameTranslationRepository> _repo = new();
    private readonly GameTitleResolver _sut;

    public GameTitleResolverTests()
    {
        _sut = new GameTitleResolver(_repo.Object);
    }

    [Fact]
    public async Task EnrichAsync_EmptyInput_ReturnsEmpty_NoRepoCall()
    {
        // Arrange: zero games in input
        var input = Array.Empty<SharedGameDto>();

        // Act
        var result = await _sut.EnrichAsync(input, CancellationToken.None);

        // Assert: empty result + no batch fetch fired
        result.Should().BeEmpty();
        _repo.Verify(
            r => r.GetByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnrichAsync_BatchFetchesSingleSqlCall()
    {
        // Arrange: two games, repo returns no translations
        var g1 = MakeGame("Catan");
        var g2 = MakeGame("Wingspan");
        _repo
            .Setup(r => r.GetByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<SharedGameTranslation>>());

        // Act
        await _sut.EnrichAsync(new[] { g1, g2 }, CancellationToken.None);

        // Assert: one batched call with both ids (no N+1)
        _repo.Verify(
            r => r.GetByGameIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(g1.Id) && ids.Contains(g2.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnrichAsync_NoTranslations_DtoTranslationsEmpty()
    {
        // Arrange: one game, repo returns empty dictionary
        var g = MakeGame("Catan");
        _repo
            .Setup(r => r.GetByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<SharedGameTranslation>>());

        // Act
        var result = await _sut.EnrichAsync(new[] { g }, CancellationToken.None);

        // Assert: dto.Translations is a non-null empty list (FE consumers iterate)
        result.Should().HaveCount(1);
        result[0].Translations.Should().NotBeNull();
        result[0].Translations.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_MapsTranslationsByGameId()
    {
        // Arrange: two games, mixed locales
        var g1 = MakeGame("Catan");
        var g2 = MakeGame("Wingspan");

        var t1Italian = SharedGameTranslation.Create(
            g1.Id, Locale.Create("it"), "I Coloni di Catan", null, TranslationSource.Manual, null, SampleNow);
        var t1French = SharedGameTranslation.Create(
            g1.Id, Locale.Create("fr"), "Les Colons de Catane", null, TranslationSource.Manual, null, SampleNow);
        var t2Italian = SharedGameTranslation.Create(
            g2.Id, Locale.Create("it"), "Ali", "I tuoi punteggi", TranslationSource.Community, null, SampleNow);

        _repo
            .Setup(r => r.GetByGameIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<SharedGameTranslation>>
            {
                [g1.Id] = new[] { t1Italian, t1French },
                [g2.Id] = new[] { t2Italian }
            });

        // Act
        var result = await _sut.EnrichAsync(new[] { g1, g2 }, CancellationToken.None);

        // Assert: translations grouped by GameId, source serialized to persisted kebab string
        result.Should().HaveCount(2);

        result[0].Translations.Should().NotBeNull();
        result[0].Translations!.Should().HaveCount(2);
        result[0].Translations!.Should().Contain(t =>
            t.Locale == "it" && t.Title == "I Coloni di Catan" && t.Source == "manual");
        result[0].Translations!.Should().Contain(t =>
            t.Locale == "fr" && t.Title == "Les Colons de Catane" && t.Source == "manual");

        result[1].Translations.Should().NotBeNull();
        result[1].Translations!.Should().HaveCount(1);
        result[1].Translations![0].Title.Should().Be("Ali");
        result[1].Translations![0].Description.Should().Be("I tuoi punteggi");
        result[1].Translations![0].Source.Should().Be("community");
    }

    private static SharedGameDto MakeGame(string title) =>
        new(
            Id: Guid.NewGuid(),
            BggId: null,
            Title: title,
            YearPublished: 2020,
            Description: "desc",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 8,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: string.Empty,
            ThumbnailUrl: string.Empty,
            Status: GameStatus.Published,
            CreatedAt: DateTime.UtcNow,
            ModifiedAt: null);
}
