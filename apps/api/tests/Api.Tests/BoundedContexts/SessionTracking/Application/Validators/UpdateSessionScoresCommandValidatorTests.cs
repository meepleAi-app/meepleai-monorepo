using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Asse A semantic alignment #1896 (T10, DEC-1): validates that
/// <see cref="UpdateSessionScoresCommandValidator"/> delegates JSON schema validation
/// to the matching <see cref="IScoringStrategy"/> per <see cref="ScoreType"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdateSessionScoresCommandValidatorTests
{
    private readonly ScoringStrategyFactory _factory = new();
    private readonly UpdateSessionScoresCommandValidator _validator;

    private static readonly Guid PlayerA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayerB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public UpdateSessionScoresCommandValidatorTests()
    {
        _validator = new UpdateSessionScoresCommandValidator(_factory);
    }

    [Fact]
    public void Validate_ValidPointsScoreData_ReturnsValid()
    {
        var json = $$"""{"scores":[{"playerId":"{{PlayerA}}","points":50},{"playerId":"{{PlayerB}}","points":30}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Points, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativePointsScoreData_ReturnsInvalid()
    {
        var json = $$"""{"scores":[{"playerId":"{{PlayerA}}","points":-5}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Points, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.ScoreData)
            && e.ErrorMessage.Contains("non-negative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BinaryWinSchemaAppliedAsPoints_ReturnsInvalid()
    {
        // BinaryWin payload (results[].isWinner) submitted with ScoringType=Points (expects scores[].points)
        var binaryWinJson = $$"""{"results":[{"playerId":"{{PlayerA}}","isWinner":true}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Points, binaryWinJson);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.ScoreData));
    }

    [Fact]
    public void Validate_ValidBinaryWinScoreData_ReturnsValid()
    {
        var json = $$"""{"results":[{"playerId":"{{PlayerA}}","isWinner":true},{"playerId":"{{PlayerB}}","isWinner":false}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.BinaryWin, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidObjectivesScoreData_ReturnsValid()
    {
        var json = $$"""{"completedByPlayer":[{"playerId":"{{PlayerA}}","objectives":["Obj1","Obj2"]},{"playerId":"{{PlayerB}}","objectives":["Obj1"]}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Objectives, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidRankingScoreData_ReturnsValid()
    {
        var json = $$"""{"positions":[{"playerId":"{{PlayerA}}","position":1},{"playerId":"{{PlayerB}}","position":2}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Ranking, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RankingWithDuplicatePosition_ReturnsInvalid()
    {
        var json = $$"""{"positions":[{"playerId":"{{PlayerA}}","position":1},{"playerId":"{{PlayerB}}","position":1}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Ranking, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.ScoreData));
    }

    [Fact]
    public void Validate_EmptyScoreData_ReturnsInvalid()
    {
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Points, string.Empty);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.ScoreData));
    }

    [Fact]
    public void Validate_EmptySessionId_ReturnsInvalid()
    {
        var json = $$"""{"scores":[{"playerId":"{{PlayerA}}","points":10}]}""";
        var cmd = new UpdateSessionScoresCommand(Guid.Empty, ScoreType.Points, json);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.SessionId));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var cmd = new UpdateSessionScoresCommand(Guid.NewGuid(), ScoreType.Points, "{ not valid json");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSessionScoresCommand.ScoreData));
    }
}
