using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Api.DevTools.Scenarios;

/// <summary>
/// Loads scenario JSON files from docs/superpowers/fixtures/scenarios.
/// Used by backend mock services to share deterministic seed data with frontend.
/// </summary>
internal sealed class ScenarioLoader
{
    private readonly string _rootPath;

    /// <summary>
    /// Creates a loader. If <paramref name="rootPath"/> is null, walks up from
    /// the current AppContext.BaseDirectory looking for the fixtures dir.
    /// </summary>
    public ScenarioLoader(string? rootPath = null)
    {
        _rootPath = rootPath ?? LocateFixturesDir();
    }

    /// <summary>Loads a scenario by name. Returns an empty JsonElement if not found.</summary>
    public JsonElement Load(string scenarioName)
    {
        var path = Path.Combine(_rootPath, $"{scenarioName}.json");
        if (!File.Exists(path))
        {
            return EmptyDocument();
        }
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>Lists all scenario file names (without .json extension).</summary>
    public IEnumerable<string> ListAvailable()
    {
        if (!Directory.Exists(_rootPath))
        {
            yield break;
        }
        foreach (var file in Directory.GetFiles(_rootPath, "*.json"))
        {
            yield return Path.GetFileNameWithoutExtension(file);
        }
    }

    private static JsonElement EmptyDocument()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private static string LocateFixturesDir()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "superpowers", "fixtures", "scenarios");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "docs", "superpowers", "fixtures", "scenarios");
    }
}
