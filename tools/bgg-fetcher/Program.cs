using System.CommandLine;
using BggFetcher;

var manifestOpt = new Option<string>("--manifest", () => "prod", "Manifest name (dev, staging, prod)");
var forceOpt = new Option<bool>("--force", () => false, "Re-fetch all games");
var root = new RootCommand("BGG Fetch Tool") { manifestOpt, forceOpt };

root.SetHandler(async (string manifestName, bool force) =>
{
    var path = YamlManifestReader.GetManifestPath(manifestName);
    if (!File.Exists(path)) { Console.Error.WriteLine($"Not found: {path}"); return; }
    Console.WriteLine($"Loading: {path}");
    var manifest = YamlManifestReader.Load(path);
    var games = manifest.Catalog.Games;
    Console.WriteLine($"Found {games.Count} games\n");

    var logDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
    using var logger = new FetchLogger(logDir, manifestName, force);
    using var client = new BggFetchClient();

    for (var i = 0; i < games.Count; i++)
    {
        var entry = games[i]; var idx = i + 1;
        if (!entry.BggId.HasValue || entry.BggId <= 0) { logger.LogFail(idx, games.Count, entry.Title, 0, "no bggId"); continue; }
        var bggId = entry.BggId.Value;
        if (entry.BggEnhanced && !force) { logger.LogSkip(idx, games.Count, entry.Title, bggId); continue; }

        try
        {
            var r = await client.FetchGameAsync(bggId, CancellationToken.None);
            if (r == null) { logger.LogFail(idx, games.Count, entry.Title, bggId, "BGG returned no data"); continue; }
            entry.BggEnhanced = true;
            entry.Description = r.Description; entry.YearPublished = r.YearPublished;
            entry.MinPlayers = r.MinPlayers; entry.MaxPlayers = r.MaxPlayers;
            entry.PlayingTime = r.PlayingTime; entry.MinAge = r.MinAge;
            entry.AverageRating = r.AverageRating.HasValue ? Math.Round(r.AverageRating.Value, 2) : null;
            entry.AverageWeight = r.AverageWeight.HasValue ? Math.Round(r.AverageWeight.Value, 2) : null;
            entry.ImageUrl = r.ImageUrl; entry.ThumbnailUrl = r.ThumbnailUrl;
            entry.Categories = r.Categories.Count > 0 ? r.Categories : null;
            entry.Mechanics = r.Mechanics.Count > 0 ? r.Mechanics : null;
            entry.Designers = r.Designers.Count > 0 ? r.Designers : null;
            entry.Publishers = r.Publishers.Count > 0 ? r.Publishers : null;

            var warnings = new List<string>();
            if (r.Description == null) warnings.Add("no description");
            if (warnings.Count > 0) logger.LogWarn(idx, games.Count, entry.Title, bggId, string.Join(", ", warnings));
            else logger.LogOk(idx, games.Count, entry.Title, bggId);
        }
        catch (Exception ex) { logger.LogFail(idx, games.Count, entry.Title, bggId, ex.Message); }
    }

    Console.WriteLine($"\nSaving: {path}");
    YamlManifestReader.Save(path, manifest);
    Console.WriteLine("Done!");
}, manifestOpt, forceOpt);

return await root.InvokeAsync(args);
