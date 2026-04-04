using System.Globalization;
using System.Xml.Linq;

namespace BggFetcher;

public sealed class BggFetchClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly TimeSpan _rateDelay = TimeSpan.FromMilliseconds(550);

    public BggFetchClient(string? apiToken = null)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/"), Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MeepleAI-BggFetcher/1.0");

        // BGG requires Bearer token auth as of Jan 2026
        var token = apiToken ?? LoadTokenFromSecrets();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string? LoadTokenFromSecrets()
    {
        // Walk up to repo root, look for infra/secrets/bgg.secret
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        if (dir == null) return null;

        var secretFile = Path.Combine(dir.FullName, "infra", "secrets", "bgg.secret");
        if (!File.Exists(secretFile)) return null;

        foreach (var line in File.ReadAllLines(secretFile))
        {
            if (line.StartsWith("BGG_API_TOKEN=", StringComparison.Ordinal))
                return line["BGG_API_TOKEN=".Length..].Trim();
        }
        return null;
    }

    public async Task<BggFetchResult?> FetchGameAsync(int bggId, CancellationToken ct)
    {
        const int maxRetries = 3;
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await Task.Delay(attempt == 0 ? _rateDelay : TimeSpan.FromSeconds(2 * attempt), ct).ConfigureAwait(false);
            response = await _http.GetAsync($"thing?id={bggId}&type=boardgame,boardgameexpansion&stats=1", ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) break;
            if ((int)response.StatusCode is 429 or 202 or 503)
                continue; // retry on rate limit / retry-later / unavailable
            return null; // non-retryable error
        }
        if (response == null || !response.IsSuccessStatusCode) return null;
        var xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var item = doc.Root?.Element("item");
        if (item == null) return null;
        var title = item.Elements("name").FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value ?? "Unknown";
        return new BggFetchResult
        {
            BggId = bggId, Title = title,
            Description = item.Element("description")?.Value,
            YearPublished = ParseInt(item.Element("yearpublished")?.Attribute("value")?.Value),
            MinPlayers = ParseInt(item.Element("minplayers")?.Attribute("value")?.Value),
            MaxPlayers = ParseInt(item.Element("maxplayers")?.Attribute("value")?.Value),
            PlayingTime = ParseInt(item.Element("playingtime")?.Attribute("value")?.Value),
            MinAge = ParseInt(item.Element("minage")?.Attribute("value")?.Value),
            AverageRating = ParseDouble(item.Element("statistics")?.Element("ratings")?.Element("average")?.Attribute("value")?.Value),
            AverageWeight = ParseDouble(item.Element("statistics")?.Element("ratings")?.Element("averageweight")?.Attribute("value")?.Value),
            ImageUrl = item.Element("image")?.Value,
            ThumbnailUrl = item.Element("thumbnail")?.Value,
            Categories = ExtractLinks(item, "boardgamecategory"),
            Mechanics = ExtractLinks(item, "boardgamemechanic"),
            Designers = ExtractLinks(item, "boardgamedesigner"),
            Publishers = ExtractLinks(item, "boardgamepublisher")
        };
    }

    private static List<string> ExtractLinks(XElement item, string linkType) =>
        item.Elements("link").Where(l => l.Attribute("type")?.Value == linkType)
            .Select(l => l.Attribute("value")?.Value).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToList();

    private static int? ParseInt(string? v) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : null;
    private static double? ParseDouble(string? v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : null;
    public void Dispose() => _http.Dispose();
}

public sealed class BggFetchResult
{
    public int BggId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? PlayingTime { get; set; }
    public int? MinAge { get; set; }
    public double? AverageRating { get; set; }
    public double? AverageWeight { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> Mechanics { get; set; } = new();
    public List<string> Designers { get; set; } = new();
    public List<string> Publishers { get; set; } = new();
}
