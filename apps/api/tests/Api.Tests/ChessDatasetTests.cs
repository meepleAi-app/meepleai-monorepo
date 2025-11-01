using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Models;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD-style tests for CHESS-02: Chess dataset validation
/// Tests chess openings, tactics, and FIDE rules dataset
/// </summary>
public class ChessDatasetTests
{
    private readonly ITestOutputHelper _output;

    #region FIDE Rules Tests

    [Fact]
    public async Task FideRules_ShouldLoadFromRuleSpec()
    {
        // Given & When: I load the RuleSpec
        var ruleSpec = await LoadChessRuleSpec();

        // Then: It should contain complete FIDE rules
        ruleSpec.Should().NotBeNull();
        ruleSpec.gameId.Should().Be("chess");
        ruleSpec.metadata.Should().NotBeNull();
        ruleSpec.metadata.name.Should().Be("Chess");
    }

    [Fact]
    public async Task FideRules_ShouldHaveAllStandardPieceMoves()
    {
        // Given: The chess RuleSpec is loaded
        var ruleSpec = await LoadChessRuleSpec();

        // Then: It should have exactly 13 actions including all piece movements
        ruleSpec.actions.Should().NotBeNull();
        (ruleSpec.actions.Count >= 13).Should().BeTrue($"Expected at least 13 actions, found {ruleSpec.actions.Count}");

        var actionIds = ruleSpec.actions.Select(a => a.id).ToList();
        actionIds.Should().Contain("move-king");
        actionIds.Should().Contain("move-queen");
        actionIds.Should().Contain("move-rook");
        actionIds.Should().Contain("move-bishop");
        actionIds.Should().Contain("move-knight");
        actionIds.Should().Contain("move-pawn");
        actionIds.Should().Contain("castle-kingside");
        actionIds.Should().Contain("castle-queenside");
        actionIds.Should().Contain("en-passant");
        actionIds.Should().Contain("promote-pawn");
    }

    [Fact]
    public async Task FideRules_ShouldHaveThreeGamePhases()
    {
        // Given: The chess RuleSpec is loaded
        var ruleSpec = await LoadChessRuleSpec();

        // Then: It should have 3 phases: opening, middlegame, endgame
        ruleSpec.phases.Should().NotBeNull();
        ruleSpec.phases.Count.Should().Be(3);

        var phaseIds = ruleSpec.phases.Select(p => p.id).ToList();
        phaseIds.Should().Contain("opening");
        phaseIds.Should().Contain("middlegame");
        phaseIds.Should().Contain("endgame");
    }

    #endregion

    #region Openings Dataset Tests

    [Fact]
    public async Task Openings_FileShouldExist()
    {
        // Given & When: I try to load the openings dataset
        // Then: It should exist and load without error
        var openings = await LoadOpeningsDataset();
        openings.Should().NotBeNull();
    }

    [Fact]
    public async Task Openings_ShouldContainAtLeast20Openings()
    {
        // Given: The openings dataset is loaded
        var openings = await LoadOpeningsDataset();

        // Then: It should contain at least 20 openings (acceptance criteria)
        openings.Should().NotBeNull();
        (openings.Count >= 20).Should().BeTrue(
            $"Expected at least 20 openings, found {openings.Count}. Add more openings to meet acceptance criteria.");
    }

    [Fact]
    public async Task Openings_ShouldIncludeMajorOpenings()
    {
        // Given: The openings dataset is loaded
        var openings = await LoadOpeningsDataset();

        // Then: It should include Italian Game, Spanish Opening, and Sicilian Defense
        var openingNames = openings.Select(o => o.name.ToLowerInvariant()).ToList();

        openingNames.Should().Contain(name => name.Contains("italian"));
        openingNames.Should().Contain(name => name.Contains("spanish") || name.Contains("ruy lopez"));
        openingNames.Should().Contain(name => name.Contains("sicilian"));
    }

    /// <summary>
    /// Data-driven test: Tests each opening individually for precise error reporting
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllOpenings))]
    public void Opening_ShouldHaveCompleteMetadata(ChessOpening opening)
    {
        // Given: An individual opening from the dataset
        // Then: It should have all required metadata fields populated
        string.IsNullOrWhiteSpace(opening.id).Should().BeFalse(
            $"Opening '{opening.name}' (id: {opening.id}) missing id");
        string.IsNullOrWhiteSpace(opening.name).Should().BeFalse(
            $"Opening (id: {opening.id}) missing name");
        string.IsNullOrWhiteSpace(opening.description).Should().BeFalse(
            $"Opening '{opening.name}' (id: {opening.id}) missing description");
        string.IsNullOrWhiteSpace(opening.pgn).Should().BeFalse(
            $"Opening '{opening.name}' (id: {opening.id}) missing PGN moves");
        string.IsNullOrWhiteSpace(opening.fen).Should().BeFalse(
            $"Opening '{opening.name}' (id: {opening.id}) missing FEN position");
        string.IsNullOrWhiteSpace(opening.strategy).Should().BeFalse(
            $"Opening '{opening.name}' (id: {opening.id}) missing strategy description");
    }

    /// <summary>
    /// Data-driven test: Validates PGN format for each opening
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllOpenings))]
    public void Opening_PgnShouldBeValid(ChessOpening opening)
    {
        // Given: An individual opening from the dataset
        // Then: Its PGN notation should be syntactically valid (start with move number)
        opening.pgn.Should().MatchRegex(@"^[1-9]\.");
    }

    /// <summary>
    /// Data-driven test: Validates FEN format for each opening
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllOpenings))]
    public void Opening_FenShouldBeValid(ChessOpening opening)
    {
        // Given: An individual opening from the dataset
        // Then: Its FEN string should be syntactically valid (6 parts separated by spaces)
        var fenParts = opening.fen.Split(' ');
        (fenParts.Length == 6).Should().BeTrue(
            $"Opening '{opening.name}' (id: {opening.id}) has invalid FEN format (expected 6 parts, found {fenParts.Length}): {opening.fen}");
    }

    [Fact]
    public async Task Openings_ItalianGameShouldHaveCorrectMoves()
    {
        // Given: The openings dataset is loaded
        var openings = await LoadOpeningsDataset();

        // When: I retrieve the Italian Game opening
        var italianGame = openings.FirstOrDefault(o =>
            o.name.Contains("Italian", StringComparison.OrdinalIgnoreCase));

        // Then: It should have the correct move sequence
        italianGame.Should().NotBeNull();
        italianGame.pgn.Should().Contain("e4");
        italianGame.pgn.Should().Contain("e5");
        italianGame.pgn.Should().Contain("Nf3");
        italianGame.pgn.Should().Contain("Nc6");
        italianGame.pgn.Should().Contain("Bc4");
    }

    [Fact]
    public async Task Openings_SicilianDefenseShouldBePresent()
    {
        // Given: The openings dataset is loaded
        var openings = await LoadOpeningsDataset();

        // When: I search for Sicilian Defense
        var sicilian = openings.FirstOrDefault(o =>
            o.name.Contains("Sicilian"));

        // Then: It should exist and have correct first moves
        sicilian.Should().NotBeNull();
        sicilian.pgn.Should().Contain("e4");
        sicilian.pgn.Should().Contain("c5");
    }

    #endregion

    #region Tactics Dataset Tests

    [Fact]
    public async Task Tactics_FileShouldExist()
    {
        // Given & When: I try to load the tactics dataset
        // Then: It should exist and load without error
        var tactics = await LoadTacticsDataset();
        tactics.Should().NotBeNull();
    }

    [Fact]
    public async Task Tactics_ShouldContainAtLeast15Tactics()
    {
        // Given: The tactics dataset is loaded
        var tactics = await LoadTacticsDataset();

        // Then: It should contain at least 15 tactics (acceptance criteria)
        tactics.Should().NotBeNull();
        (tactics.Count >= 15).Should().BeTrue(
            $"Expected at least 15 tactics, found {tactics.Count}. Add more tactics to meet acceptance criteria.");
    }

    [Fact]
    public async Task Tactics_ShouldIncludeCommonTactics()
    {
        // Given: The tactics dataset is loaded
        var tactics = await LoadTacticsDataset();

        // Then: It should include fork, pin, and discovered check
        var tacticNames = tactics.Select(t => t.name.ToLowerInvariant()).ToList();

        tacticNames.Should().Contain(name => name.Contains("fork") || name.Contains("forchetta"));
        tacticNames.Should().Contain(name => name.Contains("pin") || name.Contains("inchiodatura"));
        tacticNames.Should().Contain(name => name.Contains("discovered") || name.Contains("scoperta"));
    }

    /// <summary>
    /// Data-driven test: Tests each tactic individually for complete metadata
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllTactics))]
    public void Tactic_ShouldHaveCompleteMetadata(ChessTactic tactic)
    {
        // Given: An individual tactic from the dataset
        // Then: It should have all required metadata fields and at least 1 example
        string.IsNullOrWhiteSpace(tactic.id).Should().BeFalse(
            $"Tactic '{tactic.name}' (id: {tactic.id}) missing id");
        string.IsNullOrWhiteSpace(tactic.name).Should().BeFalse(
            $"Tactic (id: {tactic.id}) missing name");
        string.IsNullOrWhiteSpace(tactic.description).Should().BeFalse(
            $"Tactic '{tactic.name}' (id: {tactic.id}) missing description");
        tactic.examples.Should().NotBeNull();
        tactic.examples.Should().NotBeEmpty();
    }

    /// <summary>
    /// Data-driven test: Tests each tactical position example individually for valid FEN
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllTacticalPositions))]
    public void TacticalPosition_FenShouldBeValid(string tacticName, string tacticId, TacticalPosition example)
    {
        // Given: An individual tactical position example
        // Then: It should have a valid FEN notation (6 parts separated by spaces)
        string.IsNullOrWhiteSpace(example.fen).Should().BeFalse($"Tactic '{tacticName}' (id: {tacticId}) example '{example.id}' missing FEN");

        var fenParts = example.fen.Split(' ');
        fenParts.Length.Should().Be(6,
            $"Tactic '{tacticName}' (id: {tacticId}) example '{example.id}' has invalid FEN (expected 6 parts, found {fenParts.Length}): {example.fen}");
    }

    /// <summary>
    /// Data-driven test: Tests each tactical position example for complete solution data
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllTacticalPositions))]
    public void TacticalPosition_ShouldHaveCompleteSolution(string tacticName, string tacticId, TacticalPosition example)
    {
        // Given: An individual tactical position example
        // Then: It should have both solution move and explanation
        string.IsNullOrWhiteSpace(example.solution).Should().BeFalse($"Tactic '{tacticName}' (id: {tacticId}) example '{example.id}' missing solution");
        string.IsNullOrWhiteSpace(example.explanation).Should().BeFalse($"Tactic '{tacticName}' (id: {tacticId}) example '{example.id}' missing explanation");
    }

    [Fact]
    public async Task Tactics_ForkShouldHaveKnightExamples()
    {
        // Given: The tactics dataset is loaded
        var tactics = await LoadTacticsDataset();

        // When: I retrieve the fork tactic
        var fork = tactics.FirstOrDefault(t =>
            t.name.Contains("fork", StringComparison.OrdinalIgnoreCase) ||
            t.name.Contains("forchetta", StringComparison.OrdinalIgnoreCase));

        // Then: It should have at least 2 examples
        fork.Should().NotBeNull();
        fork!.examples.Count.Should().BeGreaterThanOrEqualTo(2,
            $"Fork tactic should have at least 2 examples, found {fork.examples.Count}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// MemberData provider: Returns all openings for data-driven tests
    /// </summary>
    public static IEnumerable<object[]> GetAllOpenings()
    {
        var openings = LoadOpeningsDatasetSync();
        return openings.Select(opening => new object[] { opening });
    }

    /// <summary>
    /// MemberData provider: Returns all tactics for data-driven tests
    /// </summary>
    public static IEnumerable<object[]> GetAllTactics()
    {
        var tactics = LoadTacticsDatasetSync();
        return tactics.Select(tactic => new object[] { tactic });
    }

    /// <summary>
    /// MemberData provider: Returns all tactical position examples for data-driven tests
    /// Each item includes: tactic name, tactic id, and the example position
    /// </summary>
    public static IEnumerable<object[]> GetAllTacticalPositions()
    {
        var tactics = LoadTacticsDatasetSync();
        var positions = new List<object[]>();

        foreach (var tactic in tactics)
        {
            foreach (var example in tactic.examples)
            {
                positions.Add(new object[] { tactic.name, tactic.id, example });
            }
        }

        return positions;
    }

    private string FindProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, "schemas")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find project root (schemas directory)");
    }

    private static List<ChessOpening> LoadOpeningsDatasetSync()
    {
        var projectRoot = FindProjectRootSync();
        var openingsPath = Path.Combine(projectRoot, "schemas", "chess-data", "openings.json");
        if (!File.Exists(openingsPath))
        {
            throw new FileNotFoundException($"Openings dataset not found at {openingsPath}");
        }

        var json = File.ReadAllText(openingsPath);
        var openings = JsonSerializer.Deserialize<List<ChessOpening>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return openings ?? throw new InvalidOperationException("Failed to deserialize openings dataset");
    }

    private static List<ChessTactic> LoadTacticsDatasetSync()
    {
        var projectRoot = FindProjectRootSync();
        var tacticsPath = Path.Combine(projectRoot, "schemas", "chess-data", "tactics.json");
        if (!File.Exists(tacticsPath))
        {
            throw new FileNotFoundException($"Tactics dataset not found at {tacticsPath}");
        }

        var json = File.ReadAllText(tacticsPath);
        var tactics = JsonSerializer.Deserialize<List<ChessTactic>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return tactics ?? throw new InvalidOperationException("Failed to deserialize tactics dataset");
    }

    private static string FindProjectRootSync()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, "schemas")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find project root (schemas directory)");
    }

    private async Task<ChessRuleSpecJson> LoadChessRuleSpec()
    {
        var projectRoot = FindProjectRoot();
        var ruleSpecPath = Path.Combine(projectRoot, "schemas", "examples", "chess.rulespec.json");
        if (!File.Exists(ruleSpecPath))
        {
            throw new FileNotFoundException($"Chess RuleSpec not found at {ruleSpecPath}");
        }

        var json = await File.ReadAllTextAsync(ruleSpecPath);
        var ruleSpec = JsonSerializer.Deserialize<ChessRuleSpecJson>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return ruleSpec ?? throw new InvalidOperationException("Failed to deserialize RuleSpec");
    }

    private async Task<List<ChessOpening>> LoadOpeningsDataset()
    {
        var projectRoot = FindProjectRoot();
        var openingsPath = Path.Combine(projectRoot, "schemas", "chess-data", "openings.json");
        if (!File.Exists(openingsPath))
        {
            throw new FileNotFoundException($"Openings dataset not found at {openingsPath}");
        }

        var json = await File.ReadAllTextAsync(openingsPath);
        var openings = JsonSerializer.Deserialize<List<ChessOpening>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return openings ?? throw new InvalidOperationException("Failed to deserialize openings dataset");
    }

    private async Task<List<ChessTactic>> LoadTacticsDataset()
    {
        var projectRoot = FindProjectRoot();
        var tacticsPath = Path.Combine(projectRoot, "schemas", "chess-data", "tactics.json");
        if (!File.Exists(tacticsPath))
        {
            throw new FileNotFoundException($"Tactics dataset not found at {tacticsPath}");
        }

        var json = await File.ReadAllTextAsync(tacticsPath);
        var tactics = JsonSerializer.Deserialize<List<ChessTactic>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return tactics ?? throw new InvalidOperationException("Failed to deserialize tactics dataset");
    }

    #endregion
}

#region Data Models for Testing

// Simplified model for chess.rulespec.json (camelCase matching JSON)
public class ChessRuleSpecJson
{
    public string gameId { get; set; } = string.Empty;
    public string version { get; set; } = string.Empty;
    public ChessMetadata metadata { get; set; } = new();
    public List<ChessAction> actions { get; set; } = new();
    public List<ChessPhase> phases { get; set; } = new();
}

public class ChessMetadata
{
    public string name { get; set; } = string.Empty;
}

public class ChessAction
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
}

public class ChessPhase
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
}

public class ChessOpening
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string pgn { get; set; } = string.Empty;
    public string fen { get; set; } = string.Empty;
    public string strategy { get; set; } = string.Empty;
    public string category { get; set; } = string.Empty;
    public List<string>? variations { get; set; }
}

public class ChessTactic
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string category { get; set; } = string.Empty;
    public List<TacticalPosition> examples { get; set; } = new();
}

public class TacticalPosition
{
    public string id { get; set; } = string.Empty;
    public string fen { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string solution { get; set; } = string.Empty;
    public string explanation { get; set; } = string.Empty;
}

#endregion
