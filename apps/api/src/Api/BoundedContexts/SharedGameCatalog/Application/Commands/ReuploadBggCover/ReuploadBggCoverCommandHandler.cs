using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ReuploadBggCover;

/// <summary>
/// Handler di <see cref="ReuploadBggCoverCommand"/>. Flusso: carica il gioco → richiede a BGG
/// l'immagine dichiarata per il suo <c>BggId</c> → la scarica e la ri-ospita su R2 tramite il
/// downloader già sanzionato (ADR-059 §2) → persiste la chiave sull'aggregato → evict cache.
/// </summary>
/// <remarks>
/// Riusa <see cref="IBggCoverDownloader"/> invece di reimplementare il fetch: quel servizio è già
/// SSRF-pinnato e cap-ato a 10MB, ed è lo stesso usato da
/// <c>CreateSharedGameFromPdfCommandHandler</c>. Ritorna <c>null</c> (loggando) invece di lanciare,
/// quindi il null va tradotto in un conflitto esplicito.
/// </remarks>
internal sealed class ReuploadBggCoverCommandHandler : ICommandHandler<ReuploadBggCoverCommand, BggCoverResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IBggApiService _bggApiService;
    private readonly IBggCoverDownloader _coverDownloader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly ILogger<ReuploadBggCoverCommandHandler> _logger;

    public ReuploadBggCoverCommandHandler(
        ISharedGameRepository repository,
        IBggApiService bggApiService,
        IBggCoverDownloader coverDownloader,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        ILogger<ReuploadBggCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _coverDownloader = coverDownloader ?? throw new ArgumentNullException(nameof(coverDownloader));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BggCoverResult> Handle(ReuploadBggCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        if (game.BggId is not > 0)
        {
            // Stato non azionabile, non un errore del chiamante: 409, mai InvalidOperationException.
            throw new ConflictException(
                "Il gioco non ha un BggId: non esiste una cover BoardGameGeek da ri-ospitare.");
        }

        var bggId = game.BggId.Value;

        var details = await _bggApiService.GetGameDetailsAsync(bggId, cancellationToken).ConfigureAwait(false);
        if (details?.ImageUrl is not { Length: > 0 } imageUrl)
        {
            throw new ConflictException(
                $"BoardGameGeek non espone un'immagine per BggId {bggId}.");
        }

        var r2Key = await _coverDownloader
            .DownloadAndUploadAsync(bggId, imageUrl, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(r2Key))
        {
            // Il downloader logga e ritorna null su fetch/upload fallito: qui diventa esplicito,
            // altrimenti l'admin vedrebbe un 200 senza che nulla sia cambiato.
            throw new ConflictException(
                $"Download o upload della cover BGG per BggId {bggId} non riuscito.");
        }

        game.SetBggCover(r2Key);
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La colonna che il read-model risolve è cambiata → evict lista + dettaglio.
        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _cacheRetryPolicy, game.Id, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "BGG cover re-hosted for game {GameId} (BggId {BggId}) by {AdminId} → {R2Key}",
            game.Id, bggId, command.AdminId, r2Key);

        return new BggCoverResult(r2Key);
    }
}
