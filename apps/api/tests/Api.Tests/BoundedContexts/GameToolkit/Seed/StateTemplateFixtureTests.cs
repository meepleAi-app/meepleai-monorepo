using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Seed;

/// <summary>
/// Validates that every curated StateTemplate seed fixture under
/// apps/api/src/Api/BoundedContexts/GameToolkit/Seed/StateTemplates/ deserializes
/// cleanly into AiToolkitSuggestionDto. Catches drift between schema changes
/// and seed JSONs.
///
/// Issue #1748 (B19-3d).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class StateTemplateFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Locates the seed directory by walking up from the test assembly until we find the
    /// solution file (MeepleAI.Api.sln, in apps/api/).
    /// </summary>
    private static string SeedDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MeepleAI.Api.sln")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null,
            $"Could not locate solution root from BaseDirectory={AppContext.BaseDirectory}");
        // dir is now .../apps/api/ — seed is at src/Api/BoundedContexts/GameToolkit/Seed/StateTemplates
        return Path.Combine(
            dir!.FullName,
            "src", "Api",
            "BoundedContexts", "GameToolkit", "Seed", "StateTemplates");
    }

    public static IEnumerable<object[]> AllFixtureFiles()
    {
        var seedDir = SeedDirectory();
        if (!Directory.Exists(seedDir))
        {
            yield break;
        }
        foreach (var path in Directory.EnumerateFiles(seedDir, "*.json"))
        {
            yield return new object[] { Path.GetFileName(path), path };
        }
    }

    [Theory]
    [MemberData(nameof(AllFixtureFiles))]
    public void Fixture_Deserializes_ToValidDto(string fileName, string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<AiToolkitSuggestionDto>(json, JsonOptions);

        dto.Should().NotBeNull($"fixture {fileName} must deserialize");
        dto!.ToolkitName.Should().NotBeNullOrWhiteSpace($"fixture {fileName} requires ToolkitName");
        dto.DiceTools.Should().NotBeNull($"fixture {fileName} requires DiceTools (can be empty array)");
        dto.CounterTools.Should().NotBeNull($"fixture {fileName} requires CounterTools (can be empty array)");
        dto.TimerTools.Should().NotBeNull($"fixture {fileName} requires TimerTools (can be empty array)");
        dto.Overrides.Should().NotBeNull(
            $"fixture {fileName} must declare Overrides (use all-false for default game)");
        dto.Reasoning.Should().NotBeNullOrWhiteSpace(
            $"fixture {fileName} requires Reasoning (curator note or STUB marker)");
    }

    [Fact]
    public void SeedDirectory_ContainsExpectedFixtures()
    {
        var seedDir = SeedDirectory();
        var present = Directory.EnumerateFiles(seedDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        // The 7 v1 top games (B19 Phase 2 scope, post user-confirmation 2026-05-31)
        var expected = new[]
        {
            "wingspan", "puerto-rico", "catan", "power-grid",
            "zombicide-green-horde", "paleo", "codenames",
        };

        foreach (var fixture in expected)
        {
            present.Should().Contain(fixture, $"v1 top-7 fixture {fixture}.json must exist");
        }
    }
}
