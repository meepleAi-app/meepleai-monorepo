using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Image-table slice (#3447): extracts Image/FigureCaption/Table regions (with bbox) from the raw
/// Unstructured hi_res response. These are exactly the elements the normal pipeline drops (empty text) in
/// <c>UnstructuredPdfTextExtractor.MapStructuredElements</c>, so we parse the wire format directly.
/// Safe on null/empty/invalid input → empty. bbox clamped to [0,1].
/// </summary>
public static class ImageRegionExtractor
{
    /// <summary>Default anti-noise threshold (#3456): keep regions whose normalized area
    /// (Width*Height, fraction of page) is at least 3%. Filters out icons/glyphs.</summary>
    public const double DefaultMinAreaFraction = 0.03;

    /// <summary>
    /// Issue #3565: <c>Table</c> belongs here alongside the image categories. hi_res labels a table
    /// that is drawn as graphics (the #3435 target) as <c>Table</c>, not <c>Image</c> — e.g. the
    /// wingspan scorecard, whose extracted text is garbled because its labels are rotated. Excluding
    /// it meant the VLM only ever saw illustrations and the pass extracted nothing on the real corpus.
    /// False positives stay cheap: the crop-discriminator's <c>&lt;otsl&gt;</c> gate (SP3) still decides.
    /// </summary>
    private static readonly HashSet<string> RegionCategories =
        new(StringComparer.Ordinal) { "Image", "FigureCaption", "Table" };

    public static IReadOnlyList<ExtractedImageRegion> FromHiResJson(
        string? hiResJson,
        double minAreaFraction = DefaultMinAreaFraction)
    {
        if (string.IsNullOrWhiteSpace(hiResJson))
        {
            return Array.Empty<ExtractedImageRegion>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<HiResEnvelope>(hiResJson);
            if (parsed?.Elements is null)
            {
                return Array.Empty<ExtractedImageRegion>();
            }

            return parsed.Elements
                .Where(e => e.Category is not null && RegionCategories.Contains(e.Category) && e.Bbox is not null)
                .Select(e => new ExtractedImageRegion(
                    Page: e.PageNumber > 0 ? e.PageNumber : 1,
                    X: Clamp01(e.Bbox!.X),
                    Y: Clamp01(e.Bbox!.Y),
                    Width: Clamp01(e.Bbox!.Width),
                    Height: Clamp01(e.Bbox!.Height),
                    ElementType: e.Category!))
                // #3456 anti-noise: drop tiny regions (icons/glyphs) below the area threshold.
                // Area is on already-clamped values; >= keeps a region exactly at the threshold.
                .Where(r => r.Width * r.Height >= minAreaFraction)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<ExtractedImageRegion>();
        }
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

    private sealed record HiResEnvelope(
        [property: JsonPropertyName("elements")] List<HiResElement>? Elements);

    private sealed record HiResElement(
        [property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("bbox")] HiResBbox? Bbox);

    private sealed record HiResBbox(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y,
        [property: JsonPropertyName("width")] double Width,
        [property: JsonPropertyName("height")] double Height);
}

public sealed record ExtractedImageRegion(
    int Page, double X, double Y, double Width, double Height, string ElementType);
