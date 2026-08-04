using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3435 (SP4): client for the smoldocling crop-discriminator endpoint
/// <c>POST /api/v1/extract-image</c>. Sends one pre-cropped image region and reports whether it
/// is a table plus the rebuilt markdown and diagnostic fields.
/// </summary>
public interface ISmolDoclingTableExtractor
{
    /// <summary>
    /// Discriminate a single crop. <paramref name="prefilter"/> overrides the service's colorfulness
    /// pre-filter (null = service default). The endpoint degrades a non-table / init failure to a
    /// 200 with <c>is_table=false</c> (R5), so a thrown exception here is a genuine transport/5xx error.
    /// </summary>
    Task<CropTableExtractionResult> ExtractTableAsync(
        byte[] cropImage,
        bool? prefilter,
        CancellationToken cancellationToken);
}

/// <summary>Result of <c>POST /api/v1/extract-image</c> for one crop.</summary>
public sealed record CropTableExtractionResult(
    bool IsTable,
    string Reason,
    string Markdown,
    double Confidence,
    bool Prefiltered,
    bool Degenerated);
