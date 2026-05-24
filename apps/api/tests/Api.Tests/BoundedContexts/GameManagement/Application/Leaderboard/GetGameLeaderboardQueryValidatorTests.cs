using Api.BoundedContexts.GameManagement.Application.Queries.Leaderboard;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Leaderboard;

/// <summary>
/// Validator tests for <see cref="GetGameLeaderboardQuery"/> (#1467):
/// GameId/CurrentUserId required, Limit constrained to 1..50.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1467")]
public sealed class GetGameLeaderboardQueryValidatorTests
{
    private readonly GetGameLeaderboardQueryValidator _validator = new();

    [Fact]
    public void Valid_DefaultLimit_Passes()
    {
        var query = new GetGameLeaderboardQuery(Guid.NewGuid(), Guid.NewGuid());

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyGameId_Fails()
    {
        var query = new GetGameLeaderboardQuery(Guid.Empty, Guid.NewGuid());

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyCurrentUserId_Fails()
    {
        var query = new GetGameLeaderboardQuery(Guid.NewGuid(), Guid.Empty);

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void LimitZero_Fails()
    {
        var query = new GetGameLeaderboardQuery(Guid.NewGuid(), Guid.NewGuid(), Limit: 0);

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void LimitAbove50_Fails()
    {
        var query = new GetGameLeaderboardQuery(Guid.NewGuid(), Guid.NewGuid(), Limit: 51);

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void LimitWithinRange_Passes(int limit)
    {
        var query = new GetGameLeaderboardQuery(Guid.NewGuid(), Guid.NewGuid(), Limit: limit);

        _validator.Validate(query).IsValid.Should().BeTrue();
    }
}
