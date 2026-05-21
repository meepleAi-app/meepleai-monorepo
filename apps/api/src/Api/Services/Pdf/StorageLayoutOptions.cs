namespace Api.Services.Pdf;

/// <summary>
/// Feature flags driving the 5-phase storage layout migration (issue #1314 PR 2).
///
/// Phase 4 cleanup (2026-05-21): defaults flipped from Legacy/Dual → New/New
/// after the staging migration completed end-to-end (216 PDFs moved,
/// pdf_uploads/ empty, Paladins upload validated landing in pdfs/).
/// The staging overlay flags in <c>infra/secrets/storage.secret</c> are now
/// redundant but kept until the follow-up cleanup PR removes the
/// branching code altogether.
///
/// Pre-Phase-4 default (safe): Legacy + Dual + false → identical behavior to
/// PR 1 (S3 keys under <c>pdf_uploads/{resourceKey}/...</c>, reads attempt
/// both layouts to support graceful cutover).
/// </summary>
internal sealed class StorageLayoutOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json / env-var binding.
    /// </summary>
    public const string SectionName = "StorageLayout";

    /// <summary>
    /// Controls the S3 key prefix used by <c>StoreAsync</c> for new uploads.
    /// </summary>
    public StorageWriteMode WriteMode { get; init; } = StorageWriteMode.New;

    /// <summary>
    /// Controls the S3 key prefix used by Retrieve/Delete/Exists/GetPresignedDownloadUrl
    /// for existing objects. In <see cref="StorageReadMode.Dual"/> the implementation
    /// tries the new layout first and falls back to the legacy layout on miss.
    /// </summary>
    public StorageReadMode ReadMode { get; init; } = StorageReadMode.New;

    /// <summary>
    /// Enables the <c>StorageOperationOutboxBackgroundService</c> drainer.
    /// When false, the background service is registered but exits the loop
    /// after one tick — useful in dev / test environments. The outbox table
    /// + drainer infrastructure are kept for future blob-layout migrations
    /// (SessionPhoto, GameImage, etc.).
    /// </summary>
    public bool MigrationEnabled { get; init; }

    /// <summary>
    /// Layout-version label exposed via Prometheus gauge <c>storage_layout_version</c>
    /// and the <c>S3StorageHealthCheck</c> output. Derived from <see cref="WriteMode"/>.
    /// </summary>
    public string LayoutVersionLabel => WriteMode switch
    {
        StorageWriteMode.Legacy => "v1-gameId",
        StorageWriteMode.Dual => "v1-gameId-migrating",
        StorageWriteMode.New => "v2-categorized",
        _ => "unknown",
    };
}

/// <summary>
/// Controls the S3 prefix for new uploads.
/// </summary>
internal enum StorageWriteMode
{
    /// <summary>
    /// New uploads land at <c>pdf_uploads/{resourceKey}/{fileId}_{filename}</c>
    /// (pre-PR 2 behavior). Phase 0 + Phase 1 default.
    /// </summary>
    Legacy,

    /// <summary>
    /// New uploads land at BOTH legacy AND new layout (dual-write window).
    /// Optional middle phase — currently UNUSED by PR 2 but reserved for
    /// rollback safety if Phase 2 cutover needs to be aborted.
    /// </summary>
    Dual,

    /// <summary>
    /// New uploads land at <c>{category.ToS3Folder()}/{resourceKey}/{fileId}_{filename}</c>
    /// (post-migration target). Phase 2 onwards.
    /// </summary>
    New,
}

/// <summary>
/// Controls the S3 prefix used by reads (Retrieve / Delete / Exists / Presign).
/// </summary>
internal enum StorageReadMode
{
    /// <summary>
    /// Reads only look in the legacy layout. Used in Phase 4 cleanup once
    /// the legacy folder has been emptied.
    /// </summary>
    Legacy,

    /// <summary>
    /// Reads look in the new layout first, then fall back to the legacy
    /// layout on miss. Phase 0 → Phase 3 default — supports outstanding
    /// presigned URLs and any object not yet migrated.
    /// </summary>
    Dual,

    /// <summary>
    /// Reads only look in the new layout. Used in Phase 3 once the grace
    /// window for outstanding presigned URLs has elapsed.
    /// </summary>
    New,
}
