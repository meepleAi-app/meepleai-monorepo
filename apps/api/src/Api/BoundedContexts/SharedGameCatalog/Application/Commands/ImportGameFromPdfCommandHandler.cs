using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for ImportGameFromPdfCommand.
///
/// Executes a 3-step saga with compensation:
///   Step 1 — CreateSharedGameCommand  → gameId
///   Step 2 — AddDocumentToSharedGameCommand  (COMPENSATE with DeleteSharedGame on failure)
///   Step 3 — IndexDocumentCommand (fire-and-forget via Task.Run; failure = warning only)
/// </summary>
internal sealed class ImportGameFromPdfCommandHandler
    : ICommandHandler<ImportGameFromPdfCommand, ImportGameFromPdfResult>
{
    private const string PlaceholderImageUrl = "/images/game-placeholder.svg";

    private readonly IMediator _mediator;
    private readonly ILogger<ImportGameFromPdfCommandHandler> _logger;

    public ImportGameFromPdfCommandHandler(
        IMediator mediator,
        ILogger<ImportGameFromPdfCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportGameFromPdfResult> Handle(
        ImportGameFromPdfCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "ImportGameFromPdf saga started: Title={Title}, PdfDocumentId={PdfDocumentId}, User={UserId}",
            command.Metadata.Title, command.PdfDocumentId, command.RequestedBy);

        // ─── Step 1: Create SharedGame ────────────────────────────────────────
        var gameId = await CreateSharedGameAsync(command, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Step 1 complete: SharedGame created with Id={GameId}", gameId);

        // ─── Step 2: Link PDF to SharedGame (with compensation) ───────────────
        try
        {
            await LinkPdfToGameAsync(gameId, command, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Step 2 complete: PdfDocument {PdfId} linked to game {GameId}", command.PdfDocumentId, gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Step 2 FAILED: AddDocumentToSharedGame failed for game {GameId}. Running compensation (delete game).",
                gameId);

            // Compensation: delete the just-created game
            try
            {
                await _mediator.Send(new DeleteSharedGameCommand(gameId, command.RequestedBy), cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Compensation complete: SharedGame {GameId} deleted", gameId);
            }
            catch (Exception compEx)
            {
                _logger.LogError(compEx, "Compensation FAILED: could not delete SharedGame {GameId}", gameId);
            }

            throw new InvalidOperationException(
                $"Failed to link PDF to game. The game was not created. Details: {ex.Message}", ex);
        }

        // ─── Step 3: Enqueue indexing (fire-and-forget, non-blocking) ─────────
        string indexingStatus;
        string? warning = null;

        try
        {
            // Dispatch asynchronously - do not block or await with the request cancellation token
            _ = Task.Run(async () =>
            {
                try
                {
                    await _mediator.Send(
                        new IndexDocumentCommand(command.PdfDocumentId, gameId),
                        CancellationToken.None).ConfigureAwait(false);
                    _logger.LogInformation("Step 3 complete: IndexDocumentCommand dispatched for pdfId={PdfId}, gameId={GameId}",
                        command.PdfDocumentId, gameId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Step 3 async FAILED: IndexDocumentCommand failed for pdfId={PdfId}, gameId={GameId}",
                        command.PdfDocumentId, gameId);
                }
            }, CancellationToken.None);

            indexingStatus = "pending";
            _logger.LogInformation("Step 3: IndexDocumentCommand enqueued (fire-and-forget) for game {GameId}", gameId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Step 3 FAILED to enqueue indexing for game {GameId}. Game created and PDF linked, but indexing not started.",
                gameId);
            indexingStatus = "failed";
            warning = "Indicizzazione non avviata. Il gioco è stato creato ma il PDF non è ancora searchable via RAG. Riprova tra qualche minuto dall'apposita sezione.";
        }

        _logger.LogInformation(
            "ImportGameFromPdf saga completed: GameId={GameId}, IndexingStatus={Status}",
            gameId, indexingStatus);

        return new ImportGameFromPdfResult(gameId, command.PdfDocumentId, indexingStatus, warning);
    }

    private async Task<Guid> CreateSharedGameAsync(ImportGameFromPdfCommand cmd, CancellationToken ct)
    {
        var m = cmd.Metadata;
        var imageUrl = cmd.CoverImageUrl ?? PlaceholderImageUrl;

        var createCmd = new CreateSharedGameCommand(
            Title: m.Title,
            YearPublished: m.YearPublished ?? DateTime.UtcNow.Year,
            Description: m.Description ?? string.Empty,
            MinPlayers: m.MinPlayers ?? 1,
            MaxPlayers: m.MaxPlayers ?? 4,
            PlayingTimeMinutes: m.PlayingTimeMinutes ?? 60,
            MinAge: m.MinAge ?? 10,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: imageUrl,
            ThumbnailUrl: imageUrl,
            Rules: null,
            CreatedBy: cmd.RequestedBy,
            BggId: null,
            Categories: m.Categories,
            Mechanics: m.Mechanics,
            Designers: m.Designers,
            Publishers: m.Publishers
        );

        return await _mediator.Send(createCmd, ct).ConfigureAwait(false);
    }

    private async Task LinkPdfToGameAsync(Guid gameId, ImportGameFromPdfCommand cmd, CancellationToken ct)
    {
        var linkCmd = new AddDocumentToSharedGameCommand(
            SharedGameId: gameId,
            PdfDocumentId: cmd.PdfDocumentId,
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            SetAsActive: true,
            CreatedBy: cmd.RequestedBy
        );

        await _mediator.Send(linkCmd, ct).ConfigureAwait(false);
    }
}
