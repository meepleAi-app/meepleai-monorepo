using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Unit tests for GameCoreData value object.
/// Issue #1320: extracted to eliminate field duplication between SharedGame and PrivateGame.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameCoreDataTests
{
    [Fact]
    public void Create_with_valid_inputs_returns_instance()
    {
        var data = GameCoreData.Create(
            title: "Catan",
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            description: "Trade and build",
            imageUrl: "https://example.com/catan.png",
            thumbnailUrl: "https://example.com/catan-thumb.png",
            bggId: 13,
            complexityRating: 2.3m);

        data.Title.Should().Be("Catan");
        data.YearPublished.Should().Be(1995);
        data.MinPlayers.Should().Be(3);
        data.MaxPlayers.Should().Be(4);
        data.PlayingTimeMinutes.Should().Be(90);
        data.MinAge.Should().Be(10);
        data.Description.Should().Be("Trade and build");
        data.BggId.Should().Be(13);
        data.ComplexityRating.Should().Be(2.3m);
    }

    [Fact]
    public void Create_with_empty_title_throws()
    {
        var act = () => GameCoreData.Create(
            title: "",
            yearPublished: 1995,
            minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*title*");
    }

    [Fact]
    public void Create_with_whitespace_title_throws()
    {
        var act = () => GameCoreData.Create(
            title: "   ",
            yearPublished: 1995,
            minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_trims_title_whitespace()
    {
        var data = GameCoreData.Create(
            title: "  Catan  ",
            yearPublished: 1995,
            minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10);

        data.Title.Should().Be("Catan");
    }

    [Fact]
    public void Create_with_minPlayers_zero_throws()
    {
        var act = () => GameCoreData.Create(
            title: "Test", yearPublished: 2000,
            minPlayers: 0, maxPlayers: 4,
            playingTimeMinutes: 60, minAge: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*minPlayers*");
    }

    [Fact]
    public void Create_with_minPlayers_greater_than_maxPlayers_throws()
    {
        var act = () => GameCoreData.Create(
            title: "Test", yearPublished: 2000,
            minPlayers: 5, maxPlayers: 3,
            playingTimeMinutes: 60, minAge: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*maxPlayers*");
    }

    [Fact]
    public void Create_with_negative_playingTime_throws()
    {
        var act = () => GameCoreData.Create(
            title: "Test", yearPublished: 2000,
            minPlayers: 2, maxPlayers: 4,
            playingTimeMinutes: -1, minAge: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*playingTimeMinutes*");
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var a = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        var b = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Different_titles_are_not_equal()
    {
        var a = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        var b = GameCoreData.Create("Wingspan", 1995, 3, 4, 90, 10);
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithTitle_returns_new_instance_with_updated_title()
    {
        var a = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        var b = a.WithTitle("Catan: Cities & Knights");
        b.Title.Should().Be("Catan: Cities & Knights");
        a.Title.Should().Be("Catan"); // original unchanged
    }
}
