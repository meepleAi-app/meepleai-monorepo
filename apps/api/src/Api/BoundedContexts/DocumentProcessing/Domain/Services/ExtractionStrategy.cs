using System.Text.Json;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Unstructured extraction strategy (DC-2 #3419). Maps to the Python service's <c>strategy</c>
/// request field (<c>Literal["fast", "hi_res"]</c>). Default <see cref="Fast"/> — cheap, and it
/// already emits normalized coordinates (SP-B #3406); <see cref="HiRes"/> is reserved for
/// table-heavy PDFs where it grounds table regions more precisely.
/// </summary>
public enum ExtractionStrategy
{
    Fast,
    HiRes,
}

public static class ExtractionStrategyExtensions
{
    /// <summary>The wire token accepted by the Unstructured service (schemas.py <c>Literal["fast","hi_res"]</c>).</summary>
    public static string ToWireString(this ExtractionStrategy strategy) =>
        strategy == ExtractionStrategy.HiRes ? "hi_res" : "fast";
}

/// <summary>
/// Decides the extraction strategy from a PDF's <b>prior</b> <c>StructuredElementsJson</c>
/// (DC-2 #3419): <see cref="ExtractionStrategy.HiRes"/> when any element is a <c>Table</c>,
/// otherwise <see cref="ExtractionStrategy.Fast"/>. Null/empty/invalid input → Fast (safe default:
/// fresh ingests have no prior elements, and hi_res only makes sense once fast has surfaced a table).
/// </summary>
public static class ExtractionStrategyDecider
{
    public static ExtractionStrategy FromStructuredElements(string? structuredElementsJson)
    {
        if (string.IsNullOrWhiteSpace(structuredElementsJson))
        {
            return ExtractionStrategy.Fast;
        }

        try
        {
            var elements = JsonSerializer.Deserialize<List<ExtractedElement>>(structuredElementsJson);
            if (elements is null || elements.Count == 0)
            {
                return ExtractionStrategy.Fast;
            }

            return elements.Any(e => string.Equals(e.ElementType, "Table", StringComparison.Ordinal))
                ? ExtractionStrategy.HiRes
                : ExtractionStrategy.Fast;
        }
        catch (JsonException)
        {
            return ExtractionStrategy.Fast;
        }
    }
}
