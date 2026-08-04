using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a — persists an admin-supplied manual cover. Flow: load game → SSRF-pinned,
/// 10MB-capped, HTTPS-only fetch of the admin URL (#3534) → re-encode to WebP (strips the original
/// container/metadata) → R2 raw-key put at the deterministic manual key → persist the manual cover +
/// license attestation on the aggregate → evict the read-model caches. The DEC-3c license whitelist
/// is enforced by <see cref="SetManualCoverCommandValidator"/> (400) before this runs.
/// </summary>
internal sealed class SetManualCoverCommandHandler : ICommandHandler<SetManualCoverCommand, ManualCoverResult>
{
    // Base cover dimensions (2:3 portrait, BGG thumbnail convention) — mirrors the Wikidata pipeline;
    // per-context crops are generated from this base on assign.
    private const int WebpTargetWidth = 200;
    private const int WebpTargetHeight = 300;
    private const string WebpContentType = "image/webp";

    private readonly ISharedGameRepository _repository;
    private readonly SsrfSafeHttpClient _fetcher;
    private readonly IWebpVariantGenerator _webpGenerator;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly ILogger<SetManualCoverCommandHandler> _logger;

    public SetManualCoverCommandHandler(
        ISharedGameRepository repository,
        SsrfSafeHttpClient fetcher,
        IWebpVariantGenerator webpGenerator,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        ILogger<SetManualCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        _webpGenerator = webpGenerator ?? throw new ArgumentNullException(nameof(webpGenerator));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ManualCoverResult> Handle(SetManualCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        // SSRF-pinned, 10MB-capped, HTTPS-only-per-hop fetch of the admin-supplied URL (#3534 fix 5/N).
        var imageBytes = await _fetcher.DownloadImageAsync(command.SourceUrl, cancellationToken).ConfigureAwait(false);

        // Re-encode to WebP: normalizes the format AND drops the original container/metadata. The
        // license was whitelisted by the validator, so we persist the attested value below.
        var webpBytes = await _webpGenerator
            .GenerateWebpAsync(imageBytes, WebpTargetWidth, WebpTargetHeight, cancellationToken)
            .ConfigureAwait(false);

        // Write to the deterministic manual physical key via the RAW-KEY path (the categorized
        // StoreAsync would reject the slash-containing key + mint an unresolvable random key).
        var coverKey = CoverKeyBuilder.ForManual(game.Id);
        using var webpStream = new MemoryStream(webpBytes);
        var stored = await _blobStorage
            .StoreRawKeyAsync(coverKey.PhysicalKey, webpStream, WebpContentType, cancellationToken)
            .ConfigureAwait(false);
        if (!stored)
        {
            throw new ExternalServiceException(
                "Impossibile salvare la cover manuale: storage oggetti non disponibile.");
        }

        // Persist the suffix-free DB key + the copyright attestation ONLY after the object is written.
        game.SetManualCover(
            coverKey.DbKey,
            LicenseValidator.Normalize(command.License),
            command.Attribution,
            command.SourceUrl,
            command.AdminId,
            DateTime.UtcNow);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The cover columns the read-model resolves changed → evict the list + detail caches (AC-6).
        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _cacheRetryPolicy, game.Id, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Manual cover set for game {GameId} by {AdminId} from {SourceUrl} (license {License})",
            game.Id, command.AdminId, command.SourceUrl, command.License);

        var presignedUrl = await _blobStorage
            .GetPresignedUrlForRawKeyAsync(coverKey.PhysicalKey, expirySeconds: 3600)
            .ConfigureAwait(false) ?? string.Empty;

        return new ManualCoverResult(coverKey.DbKey, presignedUrl);
    }
}
