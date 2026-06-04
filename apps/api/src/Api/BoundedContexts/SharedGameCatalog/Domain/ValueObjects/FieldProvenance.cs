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
    object Value);
