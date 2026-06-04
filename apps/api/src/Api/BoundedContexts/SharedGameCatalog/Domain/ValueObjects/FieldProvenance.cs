using System.Linq;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Per-field provenance metadata captured during catalog seed enrichment.
/// Records which provider supplied the value, the source URL, the source-side
/// field identifier (e.g. Wikidata property "P577"), and when fetched.
/// </summary>
/// <param name="Provider">"wikidata" or "bgg"</param>
/// <param name="SourceUrl">Public URL of the data source</param>
/// <param name="SourceField">Provider-side field path (e.g. "P577", "link[type=boardgamemechanic]")</param>
/// <param name="FetchedAt">UTC timestamp when the field was last fetched</param>
/// <param name="Value">The raw value (boxed primitive, string, or IReadOnlyList&lt;string&gt;)</param>
public sealed record FieldProvenance(
    string Provider,
    string SourceUrl,
    string SourceField,
    DateTime FetchedAt,
    object Value)
{
    public bool Equals(FieldProvenance? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Provider, other.Provider, StringComparison.Ordinal)
            && string.Equals(SourceUrl, other.SourceUrl, StringComparison.Ordinal)
            && string.Equals(SourceField, other.SourceField, StringComparison.Ordinal)
            && FetchedAt == other.FetchedAt
            && ValuesEqual(Value, other.Value);
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Provider);
        hc.Add(SourceUrl);
        hc.Add(SourceField);
        hc.Add(FetchedAt);
        hc.Add(ValueHash(Value));
        return hc.ToHashCode();
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is string sa && b is string sb) return string.Equals(sa, sb, StringComparison.Ordinal);
        if (a is System.Collections.IEnumerable ea && b is System.Collections.IEnumerable eb)
        {
            var ea2 = ea.Cast<object?>();
            var eb2 = eb.Cast<object?>();
            return ea2.SequenceEqual(eb2);
        }
        return a.Equals(b);
    }

    private static int ValueHash(object? v)
    {
        if (v is null) return 0;
        if (v is string s) return s.GetHashCode(StringComparison.Ordinal);
        if (v is System.Collections.IEnumerable e)
        {
            var hc = new HashCode();
            foreach (var item in e) hc.Add(item);
            return hc.ToHashCode();
        }
        return v.GetHashCode();
    }
}
