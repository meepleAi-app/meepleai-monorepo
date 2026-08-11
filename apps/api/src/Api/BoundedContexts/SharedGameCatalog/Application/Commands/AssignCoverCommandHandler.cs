using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
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
/// Epic #3470 — persists an admin's per-context cover choice through the aggregate
/// (<see cref="Domain.Aggregates.SharedGame.AssignCover"/>) and the child-safe
/// <see cref="ISharedGameRepository.ReconcileCoverAssignmentsAsync"/>. Returns the
/// persisted assignment so the picker can reflect the new state.
///
/// Slice 2 (AC-6): the cover columns the read-model resolves changed, so this handler
/// evicts the SharedGameCatalog read caches (list <c>search-games</c> + detail
/// <c>shared-game:{id}</c>) across replicas — otherwise the assignment stays invisible
/// until the 15min–2h HybridCache TTL and the picker looks broken. Mirrors
/// <see cref="EnrichCatalogCover.EnrichCatalogCoverCommandHandler"/>.
///
/// #3611 — also renders the Social per-context crop from the pinned focal point:
/// unlike Card/Hero (framed client-side via <c>object-position</c>), an OpenGraph
/// crawler does not execute CSS, so Social needs an actual file. This is the first
/// production caller of <see cref="GameCoverAssignment.SetGeneratedKey"/>.
/// </summary>
internal sealed class AssignCoverCommandHandler : ICommandHandler<AssignCoverCommand, CoverAssignmentDto>
{
    private const int SocialWidth = 1200;
    private const int SocialHeight = 630;
    private const string WebpContentType = "image/webp";

    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly IWebpVariantGenerator _webpGenerator;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<AssignCoverCommandHandler> _logger;

    public AssignCoverCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        IWebpVariantGenerator webpGenerator,
        IBlobStorageService blobStorage,
        ILogger<AssignCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _webpGenerator = webpGenerator ?? throw new ArgumentNullException(nameof(webpGenerator));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CoverAssignmentDto> Handle(AssignCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        var assignment = game.AssignCover(command.Context, command.Source, command.AdminId, command.FocalX, command.FocalY);

        // #3611 — il solo contesto Social ha bisogno di un FILE: un crawler OpenGraph non esegue
        // CSS, mentre Card e Hero sono inquadrate dal browser via object-position. Il fallimento
        // è tollerato: GeneratedR2Key resta null e il resolver ricade sull'immagine base.
        if (command.Context == CoverContext.Social)
        {
            var generatedKey = await TryRenderSocialCropAsync(game, assignment, cancellationToken)
                .ConfigureAwait(false);
            if (generatedKey is not null)
            {
                assignment.SetGeneratedKey(generatedKey);
            }
        }

        await _repository.ReconcileCoverAssignmentsAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _cacheRetryPolicy, command.GameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Assigned {Source} cover to {Context} for game {GameId} by {AdminId}",
            command.Source, command.Context, command.GameId, command.AdminId);

        return new CoverAssignmentDto(assignment.Context, assignment.Source, assignment.FocalX, assignment.FocalY);
    }

    /// <summary>
    /// Renders the 1200x630 Social crop from the base cover of the assignment's pinned
    /// source, using the assignment's focal point, and uploads it to its deterministic
    /// physical key. Returns <see langword="null"/> on ANY expected failure (missing base
    /// key, unreadable source, decode failure, storage unavailable) so the caller can
    /// leave <c>GeneratedR2Key</c> unset and let the resolver fall back to the base cover —
    /// the render is best-effort and must never block the assignment save.
    /// </summary>
    private async Task<string?> TryRenderSocialCropAsync(
        SharedGame game,
        GameCoverAssignment assignment,
        CancellationToken ct)
    {
        try
        {
            var kind = assignment.Source.ToCoverKind();
            var baseDbKey = SourceDbKeyFor(game, kind);
            if (string.IsNullOrWhiteSpace(baseDbKey))
            {
                return null;
            }

            var source = await _blobStorage
                .RetrieveRawKeyAsync(CoverKeyBuilder.PhysicalKeyFor(kind, baseDbKey), ct)
                .ConfigureAwait(false);
            if (source is null)
            {
                return null;
            }

            await using (source.ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer, ct).ConfigureAwait(false);

                // Mirrors EnrichCatalogCoverCommandHandler's guard: un blob base vuoto/corrotto
                // farebbe lanciare GenerateWebpAsync un ArgumentException — meglio arrendersi
                // qui che farlo emergere come eccezione non gestita.
                if (buffer.Length == 0)
                {
                    return null;
                }

                var cropped = await _webpGenerator
                    .GenerateWebpAsync(
                        buffer.ToArray(), SocialWidth, SocialHeight,
                        assignment.FocalX, assignment.FocalY, ct)
                    .ConfigureAwait(false);

                var key = CoverKeyBuilder.ContextCropPhysicalKey(game.Id, CoverContext.Social);
                using var upload = new MemoryStream(cropped);
                var stored = await _blobStorage
                    .StoreRawKeyAsync(key, upload, WebpContentType, ct)
                    .ConfigureAwait(false);

                return stored ? key : null;
            }
        }
        // Best-effort render (mirrors RevokeManualCoverCommandHandler): la cattura non deve
        // MAI bloccare il salvataggio dell'assegnazione, quindi copre qualunque eccezione
        // imprevista tranne la cancellazione, che deve continuare a propagare.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Render del crop Social fallito per {GameId}: la cover base resta servita", game.Id);
            return null;
        }
    }

    /// <summary>
    /// Selects the aggregate property holding the base (suffix-free) DB key for a source
    /// kind. Mirrors <c>CoverUrlResolver.SourceDbKey</c>'s switch, but reads from the
    /// domain aggregate (already loaded by this handler) instead of the read-model entity.
    /// </summary>
    private static string? SourceDbKeyFor(SharedGame game, CoverKind kind) => kind switch
    {
        CoverKind.Pdf => game.PdfCoverR2Key,
        CoverKind.Bgg => game.BggCoverR2Key,
        CoverKind.Wikidata => game.WikidataCoverR2Key,
        CoverKind.Manual => game.ManualCoverR2Key,
        // CoverKind.User (L3) is per-user and never a catalog assignment source.
        _ => null,
    };
}
