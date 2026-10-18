using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for <see cref="ImportGameFromPdfCommand"/>: completes the wizard's draft game.
/// </summary>
/// <remarks>
/// #4401: this used to create the game, link the "orphaned" PDF and send <c>IndexDocumentCommand</c>
/// from a <c>Task.Run</c>. None of it worked: the upload of an orphaned PDF was refused, the link
/// never set <c>pdf_documents.shared_game_id</c> (the column the retrieval reads), and
/// <c>IndexDocumentCommand</c> had no handler, so the failure ended in the log while the wizard
/// showed the indexing as pending. Now the game exists before the upload and the upload ties and
/// indexes the PDF; what is left is the metadata. No compensation: each step can be repeated, so a
/// failed attempt is retried rather than undone.
/// </remarks>
internal sealed class ImportGameFromPdfCommandHandler
    : ICommandHandler<ImportGameFromPdfCommand, ImportGameFromPdfResult>
{
    private const int DefaultMinPlayers = 1;
    private const int DefaultMaxPlayers = 4;
    private const int DefaultPlayingTimeMinutes = 60;
    private const int DefaultMinAge = 10;
    private const string RulebookVersion = "1.0";

    private readonly IMediator _mediator;
    private readonly ISharedGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ImportGameFromPdfCommandHandler> _logger;

    public ImportGameFromPdfCommandHandler(
        IMediator mediator,
        ISharedGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ImportGameFromPdfCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportGameFromPdfResult> Handle(
        ImportGameFromPdfCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());
        if (game.Status != GameStatus.Draft)
        {
            throw new ConflictException("Only a draft game can be completed by the import wizard.");
        }

        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == command.PdfDocumentId)
            .Select(p => new { p.SharedGameId, p.ProcessingState })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());
        if (pdf.SharedGameId != command.GameId)
        {
            // Registered as the rulebook of a game it is not tied to, it would show in the game's
            // documents and never be found by the retrieval.
            throw new ConflictException("The PDF was not uploaded for this game.");
        }

        // ─── 1: the reviewed metadata ─────────────────────────────────────────
        await _mediator.Send(BuildUpdate(command, game.ImageUrl, game.ThumbnailUrl), cancellationToken).ConfigureAwait(false);

        // ─── 2: out of the skeleton state ─────────────────────────────────────
        // Reloaded: the update above saved its own copy of the aggregate, and left it tracked. Its
        // changes are saved, so the tracker can go; otherwise attaching the reloaded copy below
        // fails with "another instance with the same key is already being tracked".
        _dbContext.ChangeTracker.Clear();
        var updated = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());
        if (updated.GameDataStatus == GameDataStatus.Skeleton)
        {
            updated.CompleteFromReviewedImport(command.RequestedBy);
            _gameRepository.Update(updated);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // ─── 3: the rulebook ──────────────────────────────────────────────────
        var alreadyRegistered = await _dbContext.SharedGameDocuments
            .AsNoTracking()
            .AnyAsync(d => d.SharedGameId == command.GameId && d.PdfDocumentId == command.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (!alreadyRegistered)
        {
            await _mediator.Send(
                new AddDocumentToSharedGameCommand(
                    SharedGameId: command.GameId,
                    PdfDocumentId: command.PdfDocumentId,
                    DocumentType: SharedGameDocumentType.Rulebook,
                    Version: RulebookVersion,
                    Tags: null,
                    SetAsActive: true,
                    CreatedBy: command.RequestedBy),
                cancellationToken).ConfigureAwait(false);
        }

        var indexingStatus = pdf.ProcessingState switch
        {
            "Ready" => "ready",
            "Failed" => "failed",
            _ => "pending",
        };
        var warning = string.Equals(indexingStatus, "failed", StringComparison.Ordinal)
            ? "Il gioco è stato creato, ma l'elaborazione del PDF è fallita: il manuale non è consultabile. Ricaricalo dalla knowledge base del gioco."
            : null;

        _logger.LogInformation(
            "PDF import completed: game {GameId}, PDF {PdfId}, processing {State}",
            command.GameId, command.PdfDocumentId, pdf.ProcessingState);

        return new ImportGameFromPdfResult(command.GameId, command.PdfDocumentId, indexingStatus, warning);
    }

    private UpdateSharedGameCommand BuildUpdate(ImportGameFromPdfCommand command, string currentImageUrl, string currentThumbnailUrl)
    {
        var m = command.Metadata;
        var minPlayers = m.MinPlayers ?? DefaultMinPlayers;

        // A cover that is not an absolute http(s) URL (the wizard's previews are blob: URLs that
        // live in the admin's browser) would be saved as a dead link: keep the game's own.
        var hasCover = Uri.TryCreate(command.CoverImageUrl, UriKind.Absolute, out var cover)
            && (string.Equals(cover.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || string.Equals(cover.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));

        return new UpdateSharedGameCommand(
            GameId: command.GameId,
            Title: m.Title,
            YearPublished: m.YearPublished ?? _timeProvider.GetUtcNow().Year,
            Description: m.Description ?? string.Empty,
            MinPlayers: minPlayers,
            MaxPlayers: m.MaxPlayers ?? Math.Max(minPlayers, DefaultMaxPlayers),
            PlayingTimeMinutes: m.PlayingTimeMinutes ?? DefaultPlayingTimeMinutes,
            MinAge: m.MinAge ?? DefaultMinAge,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: hasCover ? command.CoverImageUrl! : currentImageUrl,
            ThumbnailUrl: hasCover ? command.CoverImageUrl! : currentThumbnailUrl,
            Rules: null,
            ModifiedBy: command.RequestedBy,
            Categories: m.Categories,
            Mechanics: m.Mechanics,
            Designers: m.Designers,
            Publishers: m.Publishers);
    }
}
