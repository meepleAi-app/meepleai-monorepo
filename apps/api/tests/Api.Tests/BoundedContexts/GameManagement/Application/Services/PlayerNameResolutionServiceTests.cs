using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Tests for PlayerNameResolutionService fuzzy matching.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PlayerNameResolutionServiceTests
{
    private readonly PlayerNameResolutionService _sut = new();

    [Fact]
    public void ResolvePlayer_ExactFullNameMatch_ReturnsResolved()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var players = new Dictionary<Guid, string>
        {
            { playerId, "Marco Rossi" },
            { Guid.NewGuid(), "Luca Bianchi" }
        }.AsReadOnly();

        // Act
        var result = _sut.ResolvePlayer("Marco Rossi", players);

        // Assert
        (result.IsResolved).Should().BeTrue();
        result.PlayerId.Should().Be(playerId);
        result.ResolvedName.Should().Be("Marco Rossi");
        (result.IsAmbiguous).Should().BeFalse();
    }

    [Fact]
    public void ResolvePlayer_FirstNameMatch_ReturnsResolved()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var players = new Dictionary<Guid, string>
        {
            { playerId, "Marco Rossi" },
            { Guid.NewGuid(), "Luca Bianchi" }
        }.AsReadOnly();

        // Act
        var result = _sut.ResolvePlayer("Marco", players);

        // Assert
        (result.IsResolved).Should().BeTrue();
        result.PlayerId.Should().Be(playerId);
        result.ResolvedName.Should().Be("Marco Rossi");
    }

    [Fact]
    public void ResolvePlayer_CaseInsensitiveMatch_ReturnsResolved()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var players = new Dictionary<Guid, string>
        {
            { playerId, "Marco Rossi" },
            { Guid.NewGuid(), "Luca Bianchi" }
        }.AsReadOnly();

        // Act
        var result = _sut.ResolvePlayer("marco rossi", players);

        // Assert
        (result.IsResolved).Should().BeTrue();
        result.PlayerId.Should().Be(playerId);
        result.ResolvedName.Should().Be("Marco Rossi");
    }

    [Fact]
    public void ResolvePlayer_AmbiguousFirstName_ReturnsAmbiguous()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var players = new Dictionary<Guid, string>
        {
            { player1Id, "Marco Rossi" },
            { player2Id, "Marco Bianchi" }
        }.AsReadOnly();

        // Act
        var result = _sut.ResolvePlayer("Marco", players);

        // Assert
        (result.IsAmbiguous).Should().BeTrue();
        (result.IsResolved).Should().BeFalse();
        result.Candidates.Count.Should().Be(2);
    }

    [Fact]
    public void ResolvePlayer_NoMatch_ReturnsNotFound()
    {
        // Arrange
        var players = new Dictionary<Guid, string>
        {
            { Guid.NewGuid(), "Marco Rossi" },
            { Guid.NewGuid(), "Luca Bianchi" }
        }.AsReadOnly();

        // Act
        var result = _sut.ResolvePlayer("Giovanni", players);

        // Assert
        (result.IsResolved).Should().BeFalse();
        (result.IsAmbiguous).Should().BeFalse();
        result.PlayerId.Should().BeNull();
    }
}
