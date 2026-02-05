using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Unit tests for AddPrivateGameCommand.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// Full handler integration is tested via endpoint tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class AddPrivateGameCommandHandlerTests
{
    #region Manual Game Command Tests

    [Fact]
    public void AddPrivateGameCommand_Manual_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "My Custom Game",
            MinPlayers: 2,
            MaxPlayers: 4
        );

        // Assert
        command.UserId.Should().Be(userId);
        command.Source.Should().Be("Manual");
        command.BggId.Should().BeNull();
        command.Title.Should().Be("My Custom Game");
        command.MinPlayers.Should().Be(2);
        command.MaxPlayers.Should().Be(4);
        command.YearPublished.Should().BeNull();
        command.Description.Should().BeNull();
    }

    [Fact]
    public void AddPrivateGameCommand_Manual_CreatesWithAllOptionalProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "Detailed Custom Game",
            MinPlayers: 1,
            MaxPlayers: 6,
            YearPublished: 2023,
            Description: "A custom game with full details",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.5m,
            ImageUrl: "https://example.com/image.jpg"
        );

        // Assert
        command.YearPublished.Should().Be(2023);
        command.Description.Should().Be("A custom game with full details");
        command.PlayingTimeMinutes.Should().Be(90);
        command.MinAge.Should().Be(12);
        command.ComplexityRating.Should().Be(3.5m);
        command.ImageUrl.Should().Be("https://example.com/image.jpg");
    }

    #endregion

    #region BGG Game Command Tests

    [Fact]
    public void AddPrivateGameCommand_BoardGameGeek_CreatesWithBggId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "BGG Imported Game",
            MinPlayers: 2,
            MaxPlayers: 5,
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg"
        );

        // Assert
        command.Source.Should().Be("BoardGameGeek");
        command.BggId.Should().Be(12345);
        command.ThumbnailUrl.Should().Be("https://cf.geekdo-images.com/thumb.jpg");
    }

    [Fact]
    public void AddPrivateGameCommand_BoardGameGeek_IncludesAllBggMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 54321,
            Title: "Complete BGG Game",
            MinPlayers: 3,
            MaxPlayers: 8,
            YearPublished: 2020,
            Description: "Full BGG data",
            PlayingTimeMinutes: 120,
            MinAge: 14,
            ComplexityRating: 4.2m,
            ImageUrl: "https://cf.geekdo-images.com/image.jpg",
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg"
        );

        // Assert
        command.Should().NotBeNull();
        command.BggId.Should().Be(54321);
        command.YearPublished.Should().Be(2020);
        command.ComplexityRating.Should().Be(4.2m);
    }

    #endregion

    #region Command Validation Scenarios

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(1, 100)]
    public void AddPrivateGameCommand_ValidPlayerCounts_CreatesSuccessfully(int minPlayers, int maxPlayers)
    {
        // Act
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test Game",
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers
        );

        // Assert
        command.MinPlayers.Should().Be(minPlayers);
        command.MaxPlayers.Should().Be(maxPlayers);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(5.0)]
    public void AddPrivateGameCommand_ValidComplexityRating_CreatesSuccessfully(decimal rating)
    {
        // Act
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test Game",
            MinPlayers: 2,
            MaxPlayers: 4,
            ComplexityRating: rating
        );

        // Assert
        command.ComplexityRating.Should().Be(rating);
    }

    #endregion
}
