using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.Scenarios;
using Api.Models;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of <see cref="IBggApiService"/>.
/// Returns seed data aggregated from scenario JSON files (<c>bggGames</c> array).
/// Unknown BGG IDs return null (404 semantic).
/// </summary>
internal sealed class MockBggApiService : IBggApiService
{
    private readonly IReadOnlyList<(int BggId, string Name)> _seed;

    /// <summary>Creates a mock BGG service that aggregates seeds from all scenarios.</summary>
    public MockBggApiService(ScenarioLoader scenarios)
    {
        var all = new Dictionary<int, string>();
        foreach (var scenarioName in scenarios.ListAvailable())
        {
            var root = scenarios.Load(scenarioName);
            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (!root.TryGetProperty("bggGames", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("bggId", out var idEl) &&
                    item.TryGetProperty("name", out var nameEl))
                {
                    var id = idEl.GetInt32();
                    var name = nameEl.GetString() ?? "Unknown";
                    all[id] = name;
                }
            }
        }
        _seed = all.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <inheritdoc />
    public Task<List<BggSearchResultDto>> SearchGamesAsync(
        string query,
        bool exact = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new List<BggSearchResultDto>());
        }

        var matches = _seed
            .Where(g => exact
                ? g.Name.Equals(query, System.StringComparison.OrdinalIgnoreCase)
                : g.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(g => new BggSearchResultDto(
                BggId: g.BggId,
                Name: g.Name,
                YearPublished: 2020,
                ThumbnailUrl: null,
                Type: "boardgame"))
            .ToList();

        return Task.FromResult(matches);
    }

    /// <inheritdoc />
    public Task<BggGameDetailsDto?> GetGameDetailsAsync(int bggId, CancellationToken ct = default)
    {
        var match = _seed.FirstOrDefault(g => g.BggId == bggId);
        if (match.Name is null)
        {
            return Task.FromResult<BggGameDetailsDto?>(null);
        }

        var details = new BggGameDetailsDto(
            BggId: bggId,
            Name: match.Name,
            Description: $"Mock description for {match.Name}",
            YearPublished: 2020,
            MinPlayers: 1,
            MaxPlayers: 5,
            PlayingTime: 60,
            MinPlayTime: 45,
            MaxPlayTime: 90,
            MinAge: 10,
            AverageRating: 7.5,
            BayesAverageRating: 7.2,
            UsersRated: 1000,
            AverageWeight: 2.5,
            ThumbnailUrl: null,
            ImageUrl: null,
            Categories: new List<string> { "Strategy" },
            Mechanics: new List<string> { "Hand Management" },
            Designers: new List<string> { "Mock Designer" },
            Publishers: new List<string> { "Mock Publisher" });

        return Task.FromResult<BggGameDetailsDto?>(details);
    }
}
