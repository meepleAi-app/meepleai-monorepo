using System.Text.Json;

using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="PublishMechanicCardCommand"/> (#527). Snapshots an approved analysis into a
/// new <c>MechanicCard</c>, links the analysis (<c>PublishedCardId</c>), and writes an audit-log row —
/// all in the same transaction (AC-4 atomicity). Conflicts F1–F5/F9 surface as
/// <see cref="ConflictException"/> (409); a missing analysis is 404.
/// </summary>
internal sealed class PublishMechanicCardCommandHandler
    : ICommandHandler<PublishMechanicCardCommand, PublishMechanicCardResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IMechanicCardRepository _cardRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PublishMechanicCardCommandHandler> _logger;

    public PublishMechanicCardCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IMechanicCardRepository cardRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<PublishMechanicCardCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PublishMechanicCardResponseDto> Handle(
        PublishMechanicCardCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdWithClaimsIgnoringFiltersAsync(request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException("MechanicAnalysis", request.AnalysisId.ToString());
        }

        // F1 — analysis must be in Published lifecycle state.
        if (analysis.Status != MechanicAnalysisStatus.Published)
        {
            _logger.LogWarning(
                "Publish rejected for analysis {AnalysisId}: status is {Status} (ConflictReason={ConflictReason}).",
                analysis.Id, analysis.Status, "not_published");
            throw new ConflictException(
                $"Analysis must be in Published state. Current: {analysis.Status}. Approve it first.");
        }

        // F2 — analysis already published.
        if (analysis.PublishedCardId is not null)
        {
            _logger.LogWarning(
                "Publish rejected for analysis {AnalysisId}: already published as {CardId} (ConflictReason={ConflictReason}).",
                analysis.Id, analysis.PublishedCardId, "already_published");
            throw new ConflictException(
                $"Already published as card {analysis.PublishedCardId}. To revise, suppress current and republish.");
        }

        var game = await _sharedGameRepository.GetByIdAsync(analysis.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", analysis.SharedGameId.ToString());
        }

        MechanicCardAuditAction auditAction;
        string? auditMetadata = null;

        if (request.PreviousCardId is { } previousCardId)
        {
            // AC-5 republish: the superseded card must exist, belong to this game, and be suppressed (F9).
            var previous = await _cardRepository.GetByIdIgnoringFiltersAsync(previousCardId, cancellationToken).ConfigureAwait(false);
            if (previous is null || previous.SharedGameId != analysis.SharedGameId || !previous.IsSuppressed)
            {
                _logger.LogWarning(
                    "Publish rejected: previous card {PreviousCardId} not found / wrong game / still active (ConflictReason={ConflictReason}).",
                    previousCardId, "previous_not_suppressed");
                throw new ConflictException("Previous card not found or still active. Suppress it first before republishing.");
            }

            auditAction = MechanicCardAuditAction.Revised;
            auditMetadata = JsonSerializer.Serialize(new { previous_card_id = previousCardId });
        }
        else
        {
            // F3 — an active (non-suppressed) card already exists for this game.
            var active = await _cardRepository.GetActiveByGameAsync(analysis.SharedGameId, cancellationToken).ConfigureAwait(false);
            if (active is not null)
            {
                _logger.LogWarning(
                    "Publish rejected: active card {ExistingCardId} exists for game {SharedGameId} (ConflictReason={ConflictReason}).",
                    active.Id, analysis.SharedGameId, "race_active");
                throw new ConflictException(
                    $"Active card {active.Id} exists for this game. Suppress it before publishing a new version.");
            }

            auditAction = MechanicCardAuditAction.Published;
        }

        var version = await _cardRepository.GetMaxVersionForGameAsync(analysis.SharedGameId, cancellationToken).ConfigureAwait(false) + 1;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var gameContext = new MechanicCardGameContext
        {
            SharedGameId = analysis.SharedGameId,
            SharedGameName = game.Title,
            Publisher = game.Publishers.FirstOrDefault()?.Name,
            Language = "en"
        };

        MechanicCard card;
        try
        {
            card = MechanicCard.PublishFromAnalysis(analysis, request.Title, gameContext, request.PublisherId, version, utcNow);
        }
        catch (InvalidOperationException ex)
        {
            // Defense-in-depth aggregate invariants (should be caught above) → 409.
            throw new ConflictException(ex.Message, ex);
        }

        analysis.MarkPublished(card.Id);

        await _cardRepository.AddAsync(card, cancellationToken).ConfigureAwait(false);
        _cardRepository.AddAuditLog(
            MechanicCardAuditLog.Create(card.Id, auditAction, request.PublisherId, utcNow, auditMetadata));
        _analysisRepository.Update(analysis);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // F5 — analysis edited concurrently (xmin mismatch).
            _logger.LogWarning(ex,
                "Optimistic concurrency publishing card for analysis {AnalysisId} (ConflictReason={ConflictReason}).",
                analysis.Id, "stale_concurrency");
            throw new ConflictException("Analysis was modified concurrently, refresh and retry.", ex);
        }
        catch (DbUpdateException ex)
        {
            // F4 — partial unique index (ux_mechanic_cards_active_per_game) lost the race.
            _logger.LogWarning(ex,
                "Unique index violation publishing card for game {SharedGameId} (ConflictReason={ConflictReason}).",
                analysis.SharedGameId, "race_active");
            throw new ConflictException("Card was published concurrently from another analysis. Refresh and review.", ex);
        }

        _logger.LogInformation(
            "MechanicCard {CardId} published for game {SharedGameId} from analysis {AnalysisId} by admin {PublisherId} (version {Version}).",
            card.Id, card.SharedGameId, analysis.Id, request.PublisherId, version);

        return new PublishMechanicCardResponseDto(
            CardId: card.Id,
            SharedGameId: card.SharedGameId,
            Version: card.Version,
            PublishedAt: card.PublishedAt,
            PublishedBy: card.PublishedBy,
            Title: card.Title,
            OriginAnalysisId: card.OriginAnalysisId,
            PublicCardUrl: $"/games/{card.SharedGameId}/card");
    }
}
