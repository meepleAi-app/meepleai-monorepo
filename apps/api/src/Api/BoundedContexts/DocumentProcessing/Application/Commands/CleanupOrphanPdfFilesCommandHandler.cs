using Amazon.S3;
using Amazon.S3.Model;
using Api.Infrastructure;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handles <see cref="CleanupOrphanPdfFilesCommand"/> (#4514).
/// </summary>
/// <remarks>
/// <para>
/// The files it finds were left by the <c>PdfSeeder</c> repair before #3879: each start with the
/// file missing re-uploaded it under a new key, and the new <c>FilePath</c> never reached the row.
/// Since #4445 the repair updates the row. Abandoned wizard uploads (<c>pdfs/wizard-temp/</c>), which
/// never get a row, end up here too.
/// </para>
/// <para>
/// An object is kept when ANY row points at it: every <c>pdf_documents."FilePath"</c>, filters
/// ignored, and both keys of every storage-migration outbox row, since a pending move copies the
/// legacy key to the new one. No other column holds keys under <c>pdfs/</c>: covers live under
/// <c>covers/</c>, photos under their own prefixes.
/// </para>
/// </remarks>
internal sealed class CleanupOrphanPdfFilesCommandHandler
    : ICommandHandler<CleanupOrphanPdfFilesCommand, CleanupOrphanPdfFilesResult>
{
    /// <summary>
    /// Unreferenced objects younger than this are left alone: an upload stores the file before it
    /// writes the row, and the wizard reads its temporary upload back while the admin fills the form.
    /// </summary>
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    internal const string NotS3Error =
        "Orphan PDF cleanup applies only when STORAGE_PROVIDER=s3: this provider cannot list its objects.";

    internal const string NothingReferencedError =
        "No object under pdfs/ is referenced by any row: refusing to delete (wrong database, or keys stored in another format?).";

    private static readonly string PdfPrefix = BlobCategory.Pdf.ToS3Folder() + "/";

    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupOrphanPdfFilesCommandHandler> _logger;

    public CleanupOrphanPdfFilesCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorage,
        TimeProvider timeProvider,
        ILogger<CleanupOrphanPdfFilesCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CleanupOrphanPdfFilesResult> Handle(
        CleanupOrphanPdfFilesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Same gate as EnqueueStorageMigrationCommandHandler. Behind the DevTools proxy the check
        // fails too, and the command then deletes nothing.
        if (_blobStorage is not S3BlobStorageService s3Storage)
        {
            return new CleanupOrphanPdfFilesResult(command.DryRun, 0, 0, 0, [], 0, [NotS3Error]);
        }

        var referencedKeys = await LoadReferencedKeysAsync(cancellationToken).ConfigureAwait(false);
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - MinimumAge;

        var scanned = 0;
        var referenced = 0;
        var tooRecent = 0;
        var orphanKeys = new List<string>();
        var errors = new List<string>();
        string? continuationToken = null;

        do
        {
            ListObjectsV2Response page;
            try
            {
                page = await s3Storage.S3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = s3Storage.Options.BucketName,
                    Prefix = PdfPrefix,
                    ContinuationToken = continuationToken,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex)
            {
                // Never ex.Message: it carries the bucket, the endpoint and the request id.
                _logger.LogError(ex, "Orphan PDF cleanup: listing {Prefix} failed", PdfPrefix);
                errors.Add($"Listing {PdfPrefix} failed ({ex.ErrorCode}, {ex.GetType().Name}): nothing deleted.");
                return new CleanupOrphanPdfFilesResult(command.DryRun, scanned, referenced, tooRecent, orphanKeys, 0, errors);
            }

            foreach (var s3Object in page.S3Objects ?? [])
            {
                if (s3Object.Key.EndsWith('/'))
                {
                    continue;
                }

                scanned++;
                if (referencedKeys.Contains(s3Object.Key))
                {
                    referenced++;
                }
                else if (s3Object.LastModified.ToUniversalTime() > cutoff)
                {
                    tooRecent++;
                }
                else
                {
                    orphanKeys.Add(s3Object.Key);
                }
            }

            continuationToken = page.IsTruncated ? page.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        // A sweep that finds files but no reference to any of them is more likely reading the wrong
        // database than looking at a bucket of orphans: it would empty the bucket.
        var refuse = orphanKeys.Count > 0 && referenced == 0;
        if (refuse)
        {
            errors.Add(NothingReferencedError);
        }

        var deleted = 0;
        if (!command.DryRun && !refuse)
        {
            foreach (var key in orphanKeys)
            {
                // DeleteRawKeyAsync swallows every exception, cancellation included.
                cancellationToken.ThrowIfCancellationRequested();
                if (await s3Storage.DeleteRawKeyAsync(key, cancellationToken).ConfigureAwait(false))
                {
                    deleted++;
                }
                else
                {
                    errors.Add($"Delete failed for {key}");
                }
            }
        }

        _logger.LogInformation(
            "Orphan PDF cleanup (dry run: {DryRun}): scanned {Scanned}, referenced {Referenced}, too recent {TooRecent}, orphans {Orphans}, deleted {Deleted}",
            command.DryRun, scanned, referenced, tooRecent, orphanKeys.Count, deleted);

        return new CleanupOrphanPdfFilesResult(command.DryRun, scanned, referenced, tooRecent, orphanKeys, deleted, errors);
    }

    private async Task<HashSet<string>> LoadReferencedKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        // Filters ignored: a row they hide still owns its file.
        keys.UnionWith(await _db.PdfDocuments
            .IgnoreQueryFilters()
            .Select(p => p.FilePath)
            .ToListAsync(cancellationToken).ConfigureAwait(false));

        var moves = await _db.StorageOperationOutbox
            .Select(o => new { o.LegacyKey, o.NewKey })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var move in moves)
        {
            keys.Add(move.LegacyKey);
            keys.Add(move.NewKey);
        }

        return keys;
    }
}
