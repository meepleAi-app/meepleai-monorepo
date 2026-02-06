using System.Globalization;
using System.Xml.Linq;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;

namespace Api.Infrastructure.ExternalServices.BoardGameGeek;

public static class BggXmlParser
{
    public static List<BggSearchResult> ParseSearchResults(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants("item")
                .Where(i => string.Equals(i.Attribute("type")?.Value, "boardgame", StringComparison.Ordinal))
                .Select(i => new BggSearchResult
                {
                    BggId = int.Parse(i.Attribute("id")!.Value, CultureInfo.InvariantCulture),
                    Name = i.Element("name")?.Attribute("value")?.Value ?? "Unknown",
                    YearPublished = int.TryParse(i.Element("yearpublished")?.Attribute("value")?.Value,
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ? y : null
                })
                .ToList();
        }
        catch (Exception ex) when (ex is not ExternalServiceException)
        {
            throw new ExternalServiceException("Failed to parse BGG search results", ex);
        }
    }

    public static BggGameDetails ParseGameDetails(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var item = doc.Descendants("item")
                .FirstOrDefault(i => string.Equals(i.Attribute("type")?.Value, "boardgame", StringComparison.Ordinal))
                ?? throw new NotFoundException("BoardGameGeek Game");

            var bggId = int.Parse(item.Attribute("id")!.Value, CultureInfo.InvariantCulture);
            var title = item.Elements("name")
                .FirstOrDefault(n => string.Equals(n.Attribute("type")?.Value, "primary", StringComparison.Ordinal))
                ?.Attribute("value")?.Value ?? "Unknown";

            return new BggGameDetails
            {
                BggId = bggId,
                Title = title,
                Description = item.Element("description")?.Value,
                YearPublished = ParseInt(item.Element("yearpublished")?.Attribute("value")?.Value),
                MinPlayers = ParseInt(item.Element("minplayers")?.Attribute("value")?.Value),
                MaxPlayers = ParseInt(item.Element("maxplayers")?.Attribute("value")?.Value),
                PlayingTimeMinutes = ParseInt(item.Element("playingtime")?.Attribute("value")?.Value),
                ComplexityRating = ParseDecimal(item.Element("statistics")?.Element("ratings")?.Element("averageweight")?.Attribute("value")?.Value),
                ImageUrl = item.Element("image")?.Value,
                ThumbnailUrl = item.Element("thumbnail")?.Value,
                AverageRating = ParseDecimal(item.Element("statistics")?.Element("ratings")?.Element("average")?.Attribute("value")?.Value),
                RankPosition = ParseInt(item.Element("statistics")?.Element("ratings")?.Element("ranks")?.Elements("rank")
                    .FirstOrDefault(r => string.Equals(r.Attribute("name")?.Value, "boardgame", StringComparison.Ordinal))
                    ?.Attribute("value")?.Value)
            };
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex) when (ex is not ExternalServiceException)
        {
            throw new ExternalServiceException("Failed to parse BGG game details", ex);
        }
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
}
