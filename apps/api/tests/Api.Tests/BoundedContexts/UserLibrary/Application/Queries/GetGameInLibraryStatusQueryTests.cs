using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Unit tests for GetGameInLibraryStatusQuery creation.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGameInLibraryStatusQueryTests
{
    [Fact]
    public void GetGameInLibraryStatusQuery_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var query = new GetGameInLibraryStatusQuery(userId, gameId);

        // Assert
        query.UserId.Should().Be(userId);
        query.GameId.Should().Be(gameId);
    }
}
