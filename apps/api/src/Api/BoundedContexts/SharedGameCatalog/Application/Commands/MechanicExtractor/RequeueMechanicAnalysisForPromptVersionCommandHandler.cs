using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="RequeueMechanicAnalysisForPromptVersionCommand"/>. Resolves the PDF from the
/// first citation of the game's active card, refuses when an analysis for the current prompt version is
/// already in progress, published or partially extracted, and otherwise enqueues a fresh generation.
/// </summary>
/// <remarks>
/// The unique index <c>ux_mechanic_analyses_shared_game_pdf_prompt</c> admits one row per (game, pdf, prompt)
/// with <c>status &lt;&gt; Rejected</c>, so any non-Rejected analysis for the current prompt blocks a new one:
/// Draft/InReview/Published/PartiallyExtracted ⇒ 409 (a partial extraction must be rejected first). Only an
/// absent analysis proceeds (<see cref="IMechanicAnalysisRepository.FindByPromptVersionAsync"/> never returns a
/// Rejected one), and the command is always sent without <c>ForceRegenerate</c>.
/// </remarks>
internal sealed class RequeueMechanicAnalysisForPromptVersionCommandHandler
    : IRequestHandler<RequeueMechanicAnalysisForPromptVersionCommand, MechanicAnalysisGenerationResponseDto>
{
    private readonly IMechanicCardRepository _cards;
    private readonly IMechanicAnalysisRepository _analyses;
    private readonly IMediator _mediator;
    private readonly IMechanicPromptProvider _promptProvider;
    private readonly ILogger<RequeueMechanicAnalysisForPromptVersionCommandHandler> _logger;

    public RequeueMechanicAnalysisForPromptVersionCommandHandler(
        IMechanicCardRepository cards,
        IMechanicAnalysisRepository analyses,
        IMediator mediator,
        IMechanicPromptProvider promptProvider,
        ILogger<RequeueMechanicAnalysisForPromptVersionCommandHandler> logger)
    {
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _analyses = analyses ?? throw new ArgumentNullException(nameof(analyses));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicAnalysisGenerationResponseDto> Handle(
        RequeueMechanicAnalysisForPromptVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _cards.GetActiveByGameAsync(request.SharedGameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("MechanicCard", request.SharedGameId.ToString());

        MechanicCardContent? content;
        try
        {
            content = JsonSerializer.Deserialize<MechanicCardContent>(card.Content);
        }
        catch (JsonException)
        {
            content = null;
        }

        if (content is null)
        {
            throw new ConflictException("Contenuto della card illeggibile.");
        }

        // Every citation shares the origin analysis' PdfDocumentId (same derivation as the card query).
        var pdfDocumentId = content.Claims
            .SelectMany(c => c.Citations)
            .Select(cit => cit.PdfId)
            .FirstOrDefault();

        if (pdfDocumentId == Guid.Empty)
        {
            throw new ConflictException("La card non ha citazioni che identifichino il PDF sorgente.");
        }

        var promptVersion = _promptProvider.PromptVersion;
        var existing = await _analyses
            .FindByPromptVersionAsync(request.SharedGameId, pdfDocumentId, promptVersion, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.Status is MechanicAnalysisStatus.Draft or MechanicAnalysisStatus.InReview)
            {
                throw new ConflictException(
                    $"Analisi già in corso per il prompt {promptVersion} (stato {existing.Status}).");
            }

            if (existing.Status == MechanicAnalysisStatus.Published)
            {
                throw new ConflictException(
                    $"Analisi già pubblicata per il prompt {promptVersion}: niente da rifare.");
            }

            if (existing.Status == MechanicAnalysisStatus.PartiallyExtracted)
            {
                throw new ConflictException(
                    "Esiste un'analisi parzialmente estratta per il prompt corrente: rifiutala prima di riestrarre.");
            }
        }

        _logger.LogInformation(
            "Admin {ActorId} requeueing mechanic analysis for game {SharedGameId}, PDF {PdfDocumentId}, prompt {PromptVersion}",
            request.ActorId, request.SharedGameId, pdfDocumentId, promptVersion);

        return await _mediator.Send(
            new GenerateMechanicAnalysisCommand(
                request.SharedGameId,
                pdfDocumentId,
                request.ActorId,
                request.CostCapUsd,
                ForceRegenerate: false),
            cancellationToken).ConfigureAwait(false);
    }
}
