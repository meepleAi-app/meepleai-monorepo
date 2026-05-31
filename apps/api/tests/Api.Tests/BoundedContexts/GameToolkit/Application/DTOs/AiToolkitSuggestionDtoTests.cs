using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.DTOs;

/// <summary>
/// DTO schema deserialization tests for AiToolkitSuggestionDto.
/// Issue #1745 (B19-3a) + #1746 (B19-3b): validates additive v3 schema extensions
/// (TurnTemplate Rounds/TurnsPerRound/TurnActions/Direction +
///  ScoringTemplate Categories[]) without breaking back-compat with legacy LLM output.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class AiToolkitSuggestionDtoTests
{
    // Mirror production JsonSerializerOptions: string enum + case-insensitive
    // (production GenerateJsonAsync uses ASP.NET defaults + JsonStringEnumConverter)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Deserialize_LegacyShape_WithoutNewFields_BackCompat()
    {
        const string legacyJson = """
        {
          "ToolkitName": "Catan",
          "DiceTools": [],
          "CounterTools": [],
          "TimerTools": [],
          "ScoringTemplate": {
            "Dimensions": ["Settlements", "Cities", "Roads"],
            "DefaultUnit": "points",
            "ScoreType": "Points"
          },
          "TurnTemplate": {
            "TurnOrderType": "RoundRobin",
            "Phases": ["Roll", "Trade", "Build"]
          },
          "Overrides": {
            "OverridesTurnOrder": false,
            "OverridesScoreboard": false,
            "OverridesDiceSet": false
          },
          "Reasoning": "Catan uses 2D6 with turn-based clockwise play."
        }
        """;

        var dto = JsonSerializer.Deserialize<AiToolkitSuggestionDto>(legacyJson, JsonOptions);

        dto.Should().NotBeNull();
        dto!.ScoringTemplate.Should().NotBeNull();
        dto.ScoringTemplate!.Dimensions.Should().HaveCount(3);
        dto.ScoringTemplate.Categories.Should().BeNull("legacy output omits Categories field");
        dto.TurnTemplate.Should().NotBeNull();
        dto.TurnTemplate!.Phases.Should().HaveCount(3);
        dto.TurnTemplate.Rounds.Should().BeNull("legacy output omits Rounds field");
        dto.TurnTemplate.TurnsPerRound.Should().BeNull();
        dto.TurnTemplate.TurnActions.Should().BeNull();
        dto.TurnTemplate.Direction.Should().BeNull();
    }

    [Fact]
    public void Deserialize_V3Shape_WithRichTurnAndCategories_PopulatesAdditiveFields()
    {
        // Mirrors what DeepSeek-chat produces for Wingspan after prompt v3 (B19-3a/3b)
        const string v3Json = """
        {
          "ToolkitName": "Wingspan",
          "DiceTools": [],
          "CounterTools": [],
          "TimerTools": [],
          "ScoringTemplate": {
            "Dimensions": ["Birds", "Bonus cards", "Eggs"],
            "DefaultUnit": "points",
            "ScoreType": "Points",
            "Categories": [
              { "Id": "birds", "Label": "Birds played", "Computation": "Sum", "Weight": 1 },
              { "Id": "eggs", "Label": "Eggs", "Computation": "Count", "Weight": 1 },
              { "Id": "round-goals", "Label": "End-of-round goals", "Computation": "RankBased", "Weight": 1, "Description": "5/2/1 points by rank in goal" }
            ]
          },
          "TurnTemplate": {
            "TurnOrderType": "RoundRobin",
            "Phases": ["Round 1", "Round 2", "Round 3", "Round 4"],
            "Rounds": 4,
            "TurnsPerRound": [8, 7, 6, 5],
            "TurnActions": ["play-bird", "get-food", "lay-eggs", "draw-cards"],
            "Direction": "clockwise"
          },
          "Overrides": {
            "OverridesTurnOrder": false,
            "OverridesScoreboard": false,
            "OverridesDiceSet": true
          },
          "Reasoning": "Wingspan has a deterministic 4-round structure with decreasing turn count."
        }
        """;

        var dto = JsonSerializer.Deserialize<AiToolkitSuggestionDto>(v3Json, JsonOptions);

        dto.Should().NotBeNull();

        // 3b: structured Categories
        dto!.ScoringTemplate!.Categories.Should().NotBeNull().And.HaveCount(3);
        dto.ScoringTemplate.Categories![0].Id.Should().Be("birds");
        dto.ScoringTemplate.Categories[0].Computation.Should().Be(ScoringComputation.Sum);
        dto.ScoringTemplate.Categories[1].Computation.Should().Be(ScoringComputation.Count);
        dto.ScoringTemplate.Categories[2].Computation.Should().Be(ScoringComputation.RankBased);
        dto.ScoringTemplate.Categories[2].Description.Should().NotBeNull();

        // 3a: rich TurnTemplate
        dto.TurnTemplate!.Rounds.Should().Be(4);
        dto.TurnTemplate.TurnsPerRound.Should().Equal(8, 7, 6, 5);
        dto.TurnTemplate.TurnActions.Should().Equal("play-bird", "get-food", "lay-eggs", "draw-cards");
        dto.TurnTemplate.Direction.Should().Be("clockwise");

        // Legacy fields still populated
        dto.TurnTemplate.Phases.Should().HaveCount(4);
        dto.ScoringTemplate.Dimensions.Should().HaveCount(3);
    }

    [Fact]
    public void Construct_V3_WithExplicitParameters_RoundtripsViaJson()
    {
        var scoring = new AiScoringTemplateSuggestion(
            Dimensions: new[] { "Birds" },
            DefaultUnit: "points",
            ScoreType: ScoreType.Points,
            Categories: new[]
            {
                new AiScoringCategorySuggestion("birds", "Birds played", ScoringComputation.Sum, 1, null),
            });

        var turn = new AiTurnTemplateSuggestion(
            TurnOrderType: TurnOrderType.RoundRobin,
            Phases: new[] { "Round 1", "Round 2" },
            Rounds: 2,
            TurnsPerRound: new[] { 8, 7 },
            TurnActions: new[] { "action-a", "action-b" },
            Direction: "clockwise");

        var dto = new AiToolkitSuggestionDto(
            ToolkitName: "Test",
            DiceTools: new List<AiDiceToolSuggestion>(),
            CounterTools: new List<AiCounterToolSuggestion>(),
            TimerTools: new List<AiTimerToolSuggestion>(),
            ScoringTemplate: scoring,
            TurnTemplate: turn,
            Overrides: new AiOverrideSuggestion(false, false, false),
            Reasoning: "test");

        var roundtrip = JsonSerializer.Deserialize<AiToolkitSuggestionDto>(
            JsonSerializer.Serialize(dto, JsonOptions),
            JsonOptions);

        roundtrip.Should().NotBeNull();
        roundtrip!.TurnTemplate!.TurnsPerRound.Should().Equal(8, 7);
        roundtrip.ScoringTemplate!.Categories![0].Id.Should().Be("birds");
        roundtrip.ScoringTemplate.Categories[0].Computation.Should().Be(ScoringComputation.Sum);
    }

    [Theory]
    [InlineData("Count", ScoringComputation.Count)]
    [InlineData("Sum", ScoringComputation.Sum)]
    [InlineData("RankBased", ScoringComputation.RankBased)]
    [InlineData("Custom", ScoringComputation.Custom)]
    public void Deserialize_ScoringComputation_AllEnumValues(string value, ScoringComputation expected)
    {
        var json = $$"""{ "Id": "test", "Label": "Test", "Computation": "{{value}}", "Weight": 1 }""";
        var dto = JsonSerializer.Deserialize<AiScoringCategorySuggestion>(json, JsonOptions);
        dto.Should().NotBeNull();
        dto!.Computation.Should().Be(expected);
    }
}
