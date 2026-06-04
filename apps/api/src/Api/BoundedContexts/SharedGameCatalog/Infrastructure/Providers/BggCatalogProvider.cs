using System.Globalization;
using System.Xml.Linq;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Fallback catalog provider — BGG XML API2 (boardgamegeek.com/xmlapi2/thing).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §7.2.
/// Strictly whitelisted (see <see cref="BggImportFieldFilter"/>).
/// Inter-call throttling expected from the orchestrator (1s/req) plus Polly retry exp.
/// Issue #1903.
/// </summary>
internal sealed class BggCatalogProvider : ICatalogProvider
{
    public string Name => "bgg";

    private readonly HttpClient _http;
    private readonly ILogger<BggCatalogProvider> _logger;

    public BggCatalogProvider(HttpClient http, ILogger<BggCatalogProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.BggId is null)
        {
            return CatalogProviderResult.Empty("BggId is required for BGG provider");
        }

        var url = $"xmlapi2/thing?id={query.BggId}&stats=0";

        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("BGG XML API HTTP {Status} on BggId={BggId}", (int)resp.StatusCode, query.BggId);
                return CatalogProviderResult.Empty($"HTTP {(int)resp.StatusCode}");
            }

            return ParseXml(body, query.BggId.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BGG fetch failed for BggId={BggId}", query.BggId);
            return CatalogProviderResult.Empty(ex.GetType().Name);
        }
    }

    private static CatalogProviderResult ParseXml(string body, int bggId)
    {
        var sourceUrl = $"https://boardgamegeek.com/xmlapi2/thing?id={bggId}";
        var fetchedAt = DateTime.UtcNow;
        var doc = XDocument.Parse(body);
        var item = doc.Root?.Element("item");
        if (item is null)
        {
            return CatalogProviderResult.Empty("BGG: no item element found");
        }

        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        var name = item.Elements("name").FirstOrDefault(e => string.Equals((string?)e.Attribute("type"), "primary", StringComparison.Ordinal))?.Attribute("value")?.Value;
        if (!string.IsNullOrWhiteSpace(name) && BggImportFieldFilter.AllowedFields.Contains("name"))
        {
            fields["title"] = new FieldProvenance("bgg", sourceUrl, "name[type=primary]", fetchedAt, name);
        }

        if (TryGetInt(item, "yearpublished", out var year) && BggImportFieldFilter.AllowedFields.Contains("yearpublished"))
        {
            fields["yearPublished"] = new FieldProvenance("bgg", sourceUrl, "yearpublished", fetchedAt, year);
        }
        if (TryGetInt(item, "minplayers", out var mn))
            fields["minPlayers"] = new FieldProvenance("bgg", sourceUrl, "minplayers", fetchedAt, mn);
        if (TryGetInt(item, "maxplayers", out var mx))
            fields["maxPlayers"] = new FieldProvenance("bgg", sourceUrl, "maxplayers", fetchedAt, mx);
        if (TryGetInt(item, "playingtime", out var pt))
            fields["playingTimeMinutes"] = new FieldProvenance("bgg", sourceUrl, "playingtime", fetchedAt, pt);
        if (TryGetInt(item, "minage", out var minAge))
            fields["minAge"] = new FieldProvenance("bgg", sourceUrl, "minage", fetchedAt, minAge);

        AddLinkList(fields, item, "boardgamedesigner", "designers", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamepublisher", "publishers", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgameartist", "artists", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamemechanic", "mechanics", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamecategory", "categories", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamefamily", "families", sourceUrl, fetchedAt);

        fields["bggId"] = new FieldProvenance("bgg", sourceUrl, "item/@id", fetchedAt, bggId);

        return new CatalogProviderResult(fields, body, ErrorMessage: null);
    }

    private static bool TryGetInt(XElement item, string elementName, out int value)
    {
        value = 0;
        var v = item.Element(elementName)?.Attribute("value")?.Value;
        return v is not null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void AddLinkList(
        Dictionary<string, FieldProvenance> fields,
        XElement item,
        string linkType,
        string targetField,
        string sourceUrl,
        DateTime fetchedAt)
    {
        var key = $"link[type={linkType}]";
        if (!BggImportFieldFilter.AllowedFields.Contains(key)) return;

        var values = item.Elements("link")
            .Where(e => string.Equals((string?)e.Attribute("type"), linkType, StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("value"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();

        if (values.Count > 0)
        {
            fields[targetField] = new FieldProvenance("bgg", sourceUrl, key, fetchedAt, values);
        }
    }
}
