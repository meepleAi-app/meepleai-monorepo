using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// ADR-073 follow-up (#2383): verifies that <see cref="PlayerStatisticsDto"/> roundtrips
/// cleanly through default System.Text.Json with no per-type configuration.
///
/// ADR-073 open question #2 (PR #2381 body): "C# records with IReadOnlyList&lt;T&gt; nested
/// types require explicit STJ configuration or [JsonSerializable] source generation. If STJ
/// cannot roundtrip the DTO, cache approach fails silently (no error, just no cache)."
///
/// This test pins the contract: ANY future change to the DTO that breaks STJ roundtrip
/// (e.g. adding non-default-constructible value object, switching to interface property
/// requiring polymorphism, etc.) will fail loudly here BEFORE landing on the Redis cache
/// path defined by ADR-073 Option D.
/// </summary>
[Trait("Category", "Unit")]
public class PlayerStatisticsDtoSerializationTests
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        // Match the conventions used by ASP.NET Core default options (camelCase).
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static PlayerStatisticsDto BuildSample() => new(
        TotalSessions: 42,
        TotalWins: 18,
        GamePlayCounts: new Dictionary<string, int>
        {
            ["Catan"] = 10,
            ["Wingspan"] = 8,
            ["Brass Birmingham"] = 5,
        },
        AverageScoresByGame: new Dictionary<string, double>
        {
            ["Catan"] = 8.5,
            ["Wingspan"] = 72.3,
        },
        TotalDurationMinutes: 3120,
        WinByGame: new List<GameWinStats>
        {
            new(Guid.NewGuid(), "Catan", 10, 4),
            new(Guid.NewGuid(), "Wingspan", 8, 5),
        },
        MostPlayedGames: new List<GamePlayCount>
        {
            new(Guid.NewGuid(), "Catan", 10),
            new(Guid.NewGuid(), "Wingspan", 8),
        },
        LeaderboardRank: 17,
        FavoriteAgentName: "Albert",
        WinRateTrend: new List<MonthlyWinRate>
        {
            new("2026-01", 0.40),
            new("2026-02", 0.55),
            new("2026-03", 0.625),
        });

    [Fact]
    public void PlayerStatisticsDto_RoundtripsViaDefaultSystemTextJson()
    {
        var original = BuildSample();

        var json = JsonSerializer.Serialize(original, DefaultOptions);
        var roundtripped = JsonSerializer.Deserialize<PlayerStatisticsDto>(json, DefaultOptions);

        roundtripped.Should().NotBeNull();
        roundtripped!.TotalSessions.Should().Be(42);
        roundtripped.TotalWins.Should().Be(18);
        roundtripped.TotalDurationMinutes.Should().Be(3120);
        roundtripped.LeaderboardRank.Should().Be(17);
        roundtripped.FavoriteAgentName.Should().Be("Albert");

        roundtripped.GamePlayCounts.Should().HaveCount(3);
        roundtripped.GamePlayCounts["Catan"].Should().Be(10);
        roundtripped.AverageScoresByGame["Wingspan"].Should().BeApproximately(72.3, 1e-6);

        roundtripped.WinByGame.Should().HaveCount(2);
        roundtripped.WinByGame[0].GameName.Should().Be("Catan");
        roundtripped.WinByGame[0].Won.Should().Be(4);

        roundtripped.MostPlayedGames.Should().HaveCount(2);
        roundtripped.MostPlayedGames[1].Plays.Should().Be(8);

        roundtripped.WinRateTrend.Should().HaveCount(3);
        roundtripped.WinRateTrend[2].Month.Should().Be("2026-03");
        roundtripped.WinRateTrend[2].WinRate.Should().BeApproximately(0.625, 1e-6);
    }

    [Fact]
    public void PlayerStatisticsDto_NullableFields_RoundtripCleanly()
    {
        var withNulls = new PlayerStatisticsDto(
            TotalSessions: 0,
            TotalWins: 0,
            GamePlayCounts: new Dictionary<string, int>(),
            AverageScoresByGame: new Dictionary<string, double>(),
            TotalDurationMinutes: 0,
            WinByGame: new List<GameWinStats>(),
            MostPlayedGames: new List<GamePlayCount>(),
            LeaderboardRank: null,
            FavoriteAgentName: null,
            WinRateTrend: new List<MonthlyWinRate>());

        var json = JsonSerializer.Serialize(withNulls, DefaultOptions);
        var roundtripped = JsonSerializer.Deserialize<PlayerStatisticsDto>(json, DefaultOptions);

        roundtripped.Should().NotBeNull();
        roundtripped!.LeaderboardRank.Should().BeNull();
        roundtripped.FavoriteAgentName.Should().BeNull();
        roundtripped.GamePlayCounts.Should().BeEmpty();
        roundtripped.WinByGame.Should().BeEmpty();
        roundtripped.MostPlayedGames.Should().BeEmpty();
        roundtripped.WinRateTrend.Should().BeEmpty();
    }

    [Fact]
    public void PlayerStatisticsDto_NestedRecordsWithNullableGuid_RoundtripCleanly()
    {
        // GameWinStats and GamePlayCount have Guid? GameId — verify null AND populated
        // roundtrip through STJ without source-gen.
        var dto = new PlayerStatisticsDto(
            TotalSessions: 1,
            TotalWins: 0,
            GamePlayCounts: new Dictionary<string, int>(),
            AverageScoresByGame: new Dictionary<string, double>(),
            TotalDurationMinutes: 0,
            WinByGame: new List<GameWinStats>
            {
                new(GameId: null, GameName: "Unknown game (no shared catalog entry)", Played: 1, Won: 0),
                new(GameId: Guid.NewGuid(), GameName: "Wingspan", Played: 5, Won: 3),
            },
            MostPlayedGames: new List<GamePlayCount>
            {
                new(GameId: null, GameName: "Unknown", Plays: 1),
            },
            LeaderboardRank: null,
            FavoriteAgentName: null,
            WinRateTrend: new List<MonthlyWinRate>());

        var json = JsonSerializer.Serialize(dto, DefaultOptions);
        var roundtripped = JsonSerializer.Deserialize<PlayerStatisticsDto>(json, DefaultOptions);

        roundtripped.Should().NotBeNull();
        roundtripped!.WinByGame.Should().HaveCount(2);
        roundtripped.WinByGame[0].GameId.Should().BeNull();
        roundtripped.WinByGame[1].GameId.Should().NotBeNull();
        roundtripped.MostPlayedGames[0].GameId.Should().BeNull();
    }
}
