using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of DecisoreMoveFeedback repository.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal class DecisoreMoveFeedbackRepository : RepositoryBase, IDecisoreMoveFeedbackRepository
{
    public DecisoreMoveFeedbackRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<DecisoreMoveFeedback?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<DecisoreMoveFeedbackEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<DecisoreMoveFeedback?> GetBySuggestionIdAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<DecisoreMoveFeedbackEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.SuggestionId == suggestionId, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<DecisoreMoveFeedback>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<DecisoreMoveFeedbackEntity>()
            .AsNoTracking()
            .Where(f => f.GameSessionId == gameSessionId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<DecisoreMoveFeedback>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<DecisoreMoveFeedbackEntity>()
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.SubmittedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<DecisoreMoveFeedback>> GetByAnalysisDepthAsync(string analysisDepth, int limit = 100, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<DecisoreMoveFeedbackEntity>()
            .AsNoTracking()
            .Where(f => string.Equals(f.AnalysisDepth, analysisDepth, StringComparison.Ordinal))
            .OrderByDescending(f => f.SubmittedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(DecisoreMoveFeedback feedback, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(feedback);
        var entity = MapToEntity(feedback);
        await DbContext.Set<DecisoreMoveFeedbackEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(DecisoreMoveFeedback feedback, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(feedback);
        var entity = MapToEntity(feedback);

        var tracked = DbContext.ChangeTracker.Entries<DecisoreMoveFeedbackEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<DecisoreMoveFeedbackEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task<(int totalFollowed, int winsFollowed, int totalIgnored, int winsIgnored, double winCorrelation)> GetWinCorrelationAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<DecisoreMoveFeedbackEntity>().AsNoTracking()
            .Where(f => string.Equals(f.Outcome, "Win", StringComparison.Ordinal) || string.Equals(f.Outcome, "Loss", StringComparison.Ordinal));

        if (gameSessionId.HasValue)
            query = query.Where(f => f.GameSessionId == gameSessionId.Value);
        if (since.HasValue)
            query = query.Where(f => f.SubmittedAt >= since.Value);

        var feedbacks = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var followed = feedbacks.Where(f => f.SuggestionFollowed).ToList();
        var ignored = feedbacks.Where(f => !f.SuggestionFollowed).ToList();

        var totalFollowed = followed.Count;
        var winsFollowed = followed.Count(f => string.Equals(f.Outcome, "Win", StringComparison.Ordinal));

        var totalIgnored = ignored.Count;
        var winsIgnored = ignored.Count(f => string.Equals(f.Outcome, "Win", StringComparison.Ordinal));

        var winRateFollowed = totalFollowed > 0 ? winsFollowed / (double)totalFollowed : 0;
        var winRateIgnored = totalIgnored > 0 ? winsIgnored / (double)totalIgnored : 0;
        var correlation = (winRateFollowed - winRateIgnored) * 100; // Percentage point difference

        return (totalFollowed, winsFollowed, totalIgnored, winsIgnored, correlation);
    }

    public async Task<(int total, int helpful, int neutral, int harmful, double avgRating)> GetQualityMetricsAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<DecisoreMoveFeedbackEntity>().AsNoTracking();

        if (gameSessionId.HasValue)
            query = query.Where(f => f.GameSessionId == gameSessionId.Value);
        if (since.HasValue)
            query = query.Where(f => f.SubmittedAt >= since.Value);

        var feedbacks = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (feedbacks.Count == 0)
            return (0, 0, 0, 0, 0.0);

        var total = feedbacks.Count;
        var helpful = feedbacks.Count(f => string.Equals(f.Quality, "Helpful", StringComparison.Ordinal));
        var neutral = feedbacks.Count(f => string.Equals(f.Quality, "Neutral", StringComparison.Ordinal));
        var harmful = feedbacks.Count(f => string.Equals(f.Quality, "Harmful", StringComparison.Ordinal));
        var avgRating = feedbacks.Average(f => f.Rating);

        return (total, helpful, neutral, harmful, avgRating);
    }

    private static DecisoreMoveFeedback MapToDomain(DecisoreMoveFeedbackEntity entity)
    {
        var quality = Enum.Parse<MoveQualityAssessment>(entity.Quality, ignoreCase: true);
        var outcome = Enum.Parse<GameOutcome>(entity.Outcome, ignoreCase: true);

        var feedback = DecisoreMoveFeedback.Create(
            suggestionId: entity.SuggestionId,
            gameSessionId: entity.GameSessionId,
            userId: entity.UserId,
            rating: entity.Rating,
            quality: quality,
            outcome: outcome,
            suggestionFollowed: entity.SuggestionFollowed,
            topSuggestedMove: entity.TopSuggestedMove,
            positionStrength: entity.PositionStrength,
            analysisDepth: entity.AnalysisDepth,
            comment: entity.Comment);

        var idProp = typeof(DecisoreMoveFeedback).GetProperty("Id");
        idProp?.SetValue(feedback, entity.Id);

        var submittedAtProp = typeof(DecisoreMoveFeedback).GetProperty("SubmittedAt");
        submittedAtProp?.SetValue(feedback, entity.SubmittedAt);

        return feedback;
    }

    private static DecisoreMoveFeedbackEntity MapToEntity(DecisoreMoveFeedback feedback)
    {
        return new DecisoreMoveFeedbackEntity
        {
            Id = feedback.Id,
            SuggestionId = feedback.SuggestionId,
            GameSessionId = feedback.GameSessionId,
            UserId = feedback.UserId,
            Rating = feedback.Rating,
            Quality = feedback.Quality.ToString(),
            Comment = feedback.Comment,
            Outcome = feedback.Outcome.ToString(),
            SuggestionFollowed = feedback.SuggestionFollowed,
            TopSuggestedMove = feedback.TopSuggestedMove,
            PositionStrength = feedback.PositionStrength,
            AnalysisDepth = feedback.AnalysisDepth,
            SubmittedAt = feedback.SubmittedAt
        };
    }
}
