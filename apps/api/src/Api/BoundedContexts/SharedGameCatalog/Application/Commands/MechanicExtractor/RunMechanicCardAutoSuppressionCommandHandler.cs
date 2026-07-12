using System.Text.Json;

using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Aggregates raw <c>mechanic_card_feedback</c> rows (#533) into per-card counters and auto-suppresses
/// active cards breaching the admin-tunable thresholds (#534 ME-M3.2). Reprocess-with-bumped-prompt is
/// deferred (no v2 prompt exists) — the raised <c>MechanicCardSuppressedEvent</c> signals manual review.
/// </summary>
internal sealed class RunMechanicCardAutoSuppressionCommandHandler
    : ICommandHandler<RunMechanicCardAutoSuppressionCommand, AutoSuppressionResult>
{
    // Seeded system user (00000000-…-001); suppressed_by has no FK, so this is an honest audit actor
    // for a system-initiated (non-human) suppression.
    private static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");

    private readonly IMechanicCardRepository _cardRepository;
    private readonly IConfigurationService _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunMechanicCardAutoSuppressionCommandHandler> _logger;

    public RunMechanicCardAutoSuppressionCommandHandler(
        IMechanicCardRepository cardRepository,
        IConfigurationService configuration,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<RunMechanicCardAutoSuppressionCommandHandler> logger)
    {
        _cardRepository = cardRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AutoSuppressionResult> Handle(
        RunMechanicCardAutoSuppressionCommand request, CancellationToken cancellationToken)
    {
        var enabled = await _configuration
            .GetValueAsync("MechanicCard:AutoSuppressionEnabled", true)
            .ConfigureAwait(false);
        if (!enabled)
        {
            _logger.LogInformation("Mechanic-card auto-suppression disabled via config; skipping run.");
            return new AutoSuppressionResult(0, 0);
        }

        var errorThreshold = await _configuration
            .GetValueAsync("MechanicCard:ErrorReportsThreshold", 5)
            .ConfigureAwait(false);
        var scoreThreshold = await _configuration
            .GetValueAsync("MechanicCard:FeedbackScoreThreshold", 0.5m)
            .ConfigureAwait(false);

        var aggregates = await _cardRepository
            .GetActiveCardFeedbackAggregatesAsync(cancellationToken)
            .ConfigureAwait(false);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var evaluated = 0;
        var suppressed = 0;

        foreach (var agg in aggregates)
        {
            var total = agg.NegativeCount + agg.PositiveCount;
            decimal? score = total > 0 ? (decimal)agg.PositiveCount / total : null;

            var card = await _cardRepository
                .GetByIdIgnoringFiltersAsync(agg.CardId, cancellationToken)
                .ConfigureAwait(false);
            if (card is null || card.IsSuppressed)
            {
                // Race: the card was suppressed between the aggregate scan and this load.
                continue;
            }

            evaluated++;
            card.ApplyFeedbackAggregates(agg.NegativeCount, score, utcNow);

            var breach = agg.NegativeCount >= errorThreshold
                && score.HasValue
                && score.Value < scoreThreshold;
            if (breach)
            {
                var reason =
                    $"auto_feedback: {agg.NegativeCount} error reports, feedback score {score!.Value:0.00} below {scoreThreshold:0.00} threshold";
                card.Suppress(SystemActorId, reason, utcNow);

                var metadata = JsonSerializer.Serialize(new
                {
                    source = "auto_feedback",
                    errorReports = agg.NegativeCount,
                    feedbackScore = score.Value,
                    errorThreshold,
                    scoreThreshold
                });
                _cardRepository.AddAuditLog(
                    MechanicCardAuditLog.Create(card.Id, MechanicCardAuditAction.Suppressed, SystemActorId, utcNow, metadata));
                suppressed++;
            }

            _cardRepository.Update(card);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex, "Concurrency conflict auto-processing mechanic card {CardId}; skipping.", agg.CardId);
            }
        }

        _logger.LogInformation(
            "Mechanic-card auto-suppression complete: evaluated={Evaluated}, suppressed={Suppressed}.",
            evaluated, suppressed);
        return new AutoSuppressionResult(evaluated, suppressed);
    }
}
