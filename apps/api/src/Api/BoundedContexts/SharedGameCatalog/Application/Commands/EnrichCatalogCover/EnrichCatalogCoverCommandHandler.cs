using Amazon.S3;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Handler for <see cref="EnrichCatalogCoverCommand"/>. Orchestrates the Phase B
/// services (M3 Wikidata SPARQL + M4 Commons license/image + M6 WebP variant +
/// M7 R2 upload) per the ADR <c>adr-2026-06-09-wikidata-enrichment-architecture.md</c>
/// flow. Issue #1823 Phase B M8.
/// </summary>
/// <remarks>
/// <para>
/// Idempotency: the R2 key is deterministic
/// (<c>covers/{gameId}/cover.webp</c> physical / <c>covers/{gameId}/cover</c>
/// stored — see <see cref="ICoverR2UploadPipeline"/> XML doc) so a retry after
/// a DB save failure simply overwrites the same object. The handler therefore
/// does NOT require an atomic-audit envelope around the side-effecting R2
/// upload — see feedback memory
/// <c>atomic_audit_and_external_side_effects</c>.
/// </para>
/// <para>
/// Freshness window: ADR DEC-3i. Games with
/// <see cref="SharedGameCatalog.Domain.Aggregates.SharedGame.WikidataQidLastVerifiedAt"/>
/// &lt; 90 days old are skipped (re-verification cron will handle them later).
/// Older entries trigger a refresh of all four cover columns.
/// </para>
/// </remarks>
internal sealed class EnrichCatalogCoverCommandHandler
    : ICommandHandler<EnrichCatalogCoverCommand, EnrichCatalogCoverResult>
{
    /// <summary>
    /// Days between successive enrichment runs for the same game. ADR DEC-3i
    /// targets quarterly cadence — chosen as 90 days here to leave a small
    /// safety margin against the M15 cron drift.
    /// </summary>
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(90);

    private const string MetricTagOutcome = "outcome";
    private const string OutcomeSuccess = "success";
    private const string OutcomeSkipped = "skipped";
    private const string OutcomeFailed = "failed";

    // Stable, machine-readable skip / failure reasons. Surfaced on the result
    // record AND as metric attribute tags so dashboards can break down
    // outcomes by cause.
    public const string SkipReasonQidMissing = "qid-missing";
    public const string SkipReasonAlreadyEnrichedRecent = "already-enriched-recent";
    public const string SkipReasonImageNotAvailable = "image-not-available-p18";
    public const string SkipReasonLicenseNotWhitelisted = "license-not-whitelisted";
    public const string SkipReasonImageBytesNotAvailable = "image-bytes-not-available";
    public const string FailReasonImageProcessing = "image-processing-error";
    public const string FailReasonR2Upload = "r2-upload-error";

    private const int WebpTargetWidth = 200;
    private const int WebpTargetHeight = 300;

    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WikidataCatalogProvider _wikidataProvider;
    private readonly IWikimediaCommonsClient _commonsClient;
    private readonly IWebpVariantGenerator _webpGenerator;
    private readonly ICoverR2UploadPipeline _r2UploadPipeline;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EnrichCatalogCoverCommandHandler> _logger;

    public EnrichCatalogCoverCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        WikidataCatalogProvider wikidataProvider,
        IWikimediaCommonsClient commonsClient,
        IWebpVariantGenerator webpGenerator,
        ICoverR2UploadPipeline r2UploadPipeline,
        TimeProvider timeProvider,
        ILogger<EnrichCatalogCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _wikidataProvider = wikidataProvider ?? throw new ArgumentNullException(nameof(wikidataProvider));
        _commonsClient = commonsClient ?? throw new ArgumentNullException(nameof(commonsClient));
        _webpGenerator = webpGenerator ?? throw new ArgumentNullException(nameof(webpGenerator));
        _r2UploadPipeline = r2UploadPipeline ?? throw new ArgumentNullException(nameof(r2UploadPipeline));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichCatalogCoverResult> Handle(
        EnrichCatalogCoverCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load aggregate. Game-not-found → 404 (pitfall #2568).
        var game = await _repository
            .GetByIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new NotFoundException("SharedGame", request.GameId.ToString());
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // 2. Freshness window (DEC-3i preparation). Both the column AND a non-null
        //    R2 key are required to short-circuit — a verified-but-cleared entry
        //    must still re-enrich. Wave 3 M12: callers (admin trigger) can pass
        //    ForceRefresh=true to bypass the window for dogfood / edge-case
        //    re-runs; the scheduler still passes the default false.
        if (!request.ForceRefresh
            && game.WikidataQidLastVerifiedAt is { } lastVerifiedAt
            && !string.IsNullOrWhiteSpace(game.WikidataCoverR2Key)
            && (nowUtc - lastVerifiedAt) < FreshnessWindow)
        {
            EmitOutcomeMetric(OutcomeSkipped, SkipReasonAlreadyEnrichedRecent);
            return new EnrichCatalogCoverResult.Skipped(SkipReasonAlreadyEnrichedRecent);
        }

        // 3. WikidataQid must already be assigned (e.g. by the M9 scheduler or
        //    by the catalog seed promotion pipeline). Without it the SPARQL
        //    query has nothing to bind.
        if (string.IsNullOrWhiteSpace(game.WikidataQid))
        {
            EmitOutcomeMetric(OutcomeSkipped, SkipReasonQidMissing);
            return new EnrichCatalogCoverResult.Skipped(SkipReasonQidMissing);
        }

        // 4. M3 — Wikidata SPARQL wdt:P18 lookup.
        var coverImage = await _wikidataProvider
            .FetchCoverImageAsync(game.WikidataQid, cancellationToken)
            .ConfigureAwait(false);

        if (!coverImage.HasImage || string.IsNullOrWhiteSpace(coverImage.Filename))
        {
            EmitOutcomeMetric(OutcomeSkipped, SkipReasonImageNotAvailable);
            _logger.LogInformation(
                "SharedGame {GameId} QID {Qid} has no wdt:P18 claim; skipping enrichment.",
                game.Id, game.WikidataQid);
            return new EnrichCatalogCoverResult.Skipped(SkipReasonImageNotAvailable);
        }

        // 5. M4 — Commons imageinfo license fetch + DEC-3c whitelist gate.
        var licenseResult = await _commonsClient
            .FetchLicenseAsync(coverImage.Filename, cancellationToken)
            .ConfigureAwait(false);

        if (!licenseResult.IsWhitelisted || string.IsNullOrWhiteSpace(licenseResult.RawLicense))
        {
            EmitOutcomeMetric(OutcomeSkipped, SkipReasonLicenseNotWhitelisted);
            _logger.LogInformation(
                "SharedGame {GameId} QID {Qid} Commons file '{Filename}' license '{License}' not whitelisted; skipping.",
                game.Id, game.WikidataQid, coverImage.Filename, licenseResult.RawLicense);
            return new EnrichCatalogCoverResult.Skipped(SkipReasonLicenseNotWhitelisted);
        }

        // 6. M4 extension — Commons Special:FilePath image bytes download.
        var originalBytes = await _commonsClient
            .FetchImageBytesAsync(coverImage.Filename, cancellationToken)
            .ConfigureAwait(false);

        if (originalBytes is null || originalBytes.Length == 0)
        {
            EmitOutcomeMetric(OutcomeSkipped, SkipReasonImageBytesNotAvailable);
            _logger.LogWarning(
                "SharedGame {GameId} QID {Qid} Commons file '{Filename}' image bytes not available; skipping.",
                game.Id, game.WikidataQid, coverImage.Filename);
            return new EnrichCatalogCoverResult.Skipped(SkipReasonImageBytesNotAvailable);
        }

        // 7. M6 — WebP variant generation (200x300 center-crop, quality 85).
        byte[] webpBytes;
        try
        {
            webpBytes = await _webpGenerator
                .GenerateWebpAsync(originalBytes, WebpTargetWidth, WebpTargetHeight, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ImageProcessingException ex)
        {
            EmitOutcomeMetric(OutcomeFailed, FailReasonImageProcessing);
            _logger.LogWarning(ex,
                "SharedGame {GameId} QID {Qid} WebP encoding failed for file '{Filename}'.",
                game.Id, game.WikidataQid, coverImage.Filename);
            return new EnrichCatalogCoverResult.Failed(FailReasonImageProcessing, ex.Message);
        }

        // 8. M7 — R2 upload. The pipeline returns the suffix-stripped key
        //    contract per CoverUrlResolver.cs:73-79.
        string r2Key;
        try
        {
            r2Key = await _r2UploadPipeline
                .UploadAsync(game.Id, webpBytes, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            EmitOutcomeMetric(OutcomeFailed, FailReasonR2Upload);
            _logger.LogWarning(ex,
                "SharedGame {GameId} QID {Qid} R2 upload failed.",
                game.Id, game.WikidataQid);
            return new EnrichCatalogCoverResult.Failed(FailReasonR2Upload, ex.Message);
        }

        // 9. Domain state update + persistence.
        game.SetWikidataCover(
            r2Key,
            licenseResult.RawLicense,
            licenseResult.Attribution,
            coverImage.SourceUrl,
            nowUtc);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 10. Success metric + result.
        EmitOutcomeMetric(OutcomeSuccess);
        _logger.LogInformation(
            "SharedGame {GameId} QID {Qid} enriched with cover key '{R2Key}' license '{License}'.",
            game.Id, game.WikidataQid, r2Key, licenseResult.RawLicense);

        return new EnrichCatalogCoverResult.Success(
            r2Key,
            licenseResult.RawLicense,
            licenseResult.Attribution,
            coverImage.SourceUrl);
    }

    /// <summary>
    /// Emits the <c>meepleai.wikidata.enrichment.attempts.total</c> counter
    /// tagged by outcome. ADR DEC-3g.
    /// </summary>
    private static void EmitOutcomeMetric(string outcome, string? reason = null)
    {
        if (reason is null)
        {
            MeepleAiMetrics.WikidataEnrichmentAttempts.Add(
                1,
                new KeyValuePair<string, object?>(MetricTagOutcome, outcome));
        }
        else
        {
            MeepleAiMetrics.WikidataEnrichmentAttempts.Add(
                1,
                new KeyValuePair<string, object?>(MetricTagOutcome, outcome),
                new KeyValuePair<string, object?>("reason", reason));
        }
    }
}
