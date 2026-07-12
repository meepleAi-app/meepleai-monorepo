using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="SubmitMechanicCardFeedbackCommand"/> (#533). Records feedback idempotently
/// per (card, user, claim) and enforces a per-user-per-day cap. Card-counter/auto-suppression logic
/// lives in #534, which aggregates these rows — this handler only persists the raw feedback.
/// </summary>
internal sealed class SubmitMechanicCardFeedbackCommandHandler
    : IRequestHandler<SubmitMechanicCardFeedbackCommand, SubmitMechanicCardFeedbackResult>
{
    /// <summary>Max NEW feedback rows a user may create per rolling 24h (#533 AC). Updates are unlimited.</summary>
    internal const int MaxNewFeedbackPerDay = 10;

    private readonly MeepleAiDbContext _dbContext;

    public SubmitMechanicCardFeedbackCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SubmitMechanicCardFeedbackResult> Handle(
        SubmitMechanicCardFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Suppression query filter applies → feedback on a suppressed/absent card is a 404.
        var cardExists = await _dbContext.MechanicCards
            .AnyAsync(c => c.Id == request.CardId, cancellationToken)
            .ConfigureAwait(false);
        if (!cardExists)
        {
            return new SubmitMechanicCardFeedbackResult(SubmitFeedbackOutcome.CardNotFound);
        }

        // AsTracking is required because the DbContext defaults to NoTracking (PERF-06): without it
        // the mutate-then-SaveChanges below is a silent no-op.
        var existing = await _dbContext.MechanicCardFeedback
            .AsTracking()
            .FirstOrDefaultAsync(
                f => f.CardId == request.CardId && f.UserId == request.UserId && f.ClaimId == request.ClaimId,
                cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;

        if (existing is not null)
        {
            // Idempotent change — update in place, does NOT count against the daily cap.
            existing.IsPositive = request.IsPositive;
            existing.ErrorType = request.IsPositive ? null : request.ErrorType;
            existing.Description = request.Description;
            existing.SuggestedCitation = request.SuggestedCitation;
            existing.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SubmitMechanicCardFeedbackResult(SubmitFeedbackOutcome.Updated);
        }

        var since = now.AddDays(-1);
        var recentCount = await _dbContext.MechanicCardFeedback
            .CountAsync(f => f.UserId == request.UserId && f.CreatedAt >= since, cancellationToken)
            .ConfigureAwait(false);
        if (recentCount >= MaxNewFeedbackPerDay)
        {
            return new SubmitMechanicCardFeedbackResult(SubmitFeedbackOutcome.RateLimited);
        }

        _dbContext.MechanicCardFeedback.Add(new MechanicCardFeedbackEntity
        {
            Id = Guid.NewGuid(),
            CardId = request.CardId,
            UserId = request.UserId,
            ClaimId = request.ClaimId,
            IsPositive = request.IsPositive,
            ErrorType = request.IsPositive ? null : request.ErrorType,
            Description = request.Description,
            SuggestedCitation = request.SuggestedCitation,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SubmitMechanicCardFeedbackResult(SubmitFeedbackOutcome.Created);
    }
}
