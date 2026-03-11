using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Tests.Constants;
using Xunit;

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
        Assert.True(result.IsResolved);
        Assert.Equal(playerId, result.PlayerId);
        Assert.Equal("Marco Rossi", result.ResolvedName);
        Assert.False(result.IsAmbiguous);
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
        Assert.True(result.IsResolved);
        Assert.Equal(playerId, result.PlayerId);
        Assert.Equal("Marco Rossi", result.ResolvedName);
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
        Assert.True(result.IsResolved);
        Assert.Equal(playerId, result.PlayerId);
        Assert.Equal("Marco Rossi", result.ResolvedName);
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
        Assert.True(result.IsAmbiguous);
        Assert.False(result.IsResolved);
        Assert.Equal(2, result.Candidates.Count);
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
        Assert.False(result.IsResolved);
        Assert.False(result.IsAmbiguous);
        Assert.Null(result.PlayerId);
    }
}
