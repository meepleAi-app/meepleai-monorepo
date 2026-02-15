using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ArbitroValidationFeedback repository.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal class ArbitroValidationFeedbackRepository : RepositoryBase, IArbitroValidationFeedbackRepository
{
    public ArbitroValidationFeedbackRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ArbitroValidationFeedback?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ArbitroValidationFeedbackEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<ArbitroValidationFeedback?> GetByValidationIdAsync(
        Guid validationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ArbitroValidationFeedbackEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ValidationId == validationId, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ArbitroValidationFeedback>> GetBySessionIdAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ArbitroValidationFeedbackEntity>()
            .AsNoTracking()
            .Where(f => f.GameSessionId == gameSessionId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ArbitroValidationFeedback>> GetByUserIdAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ArbitroValidationFeedbackEntity>()
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.SubmittedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ArbitroValidationFeedback feedback, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(feedback);
        var entity = MapToEntity(feedback);
        await DbContext.Set<ArbitroValidationFeedbackEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ArbitroValidationFeedback feedback, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(feedback);
        var entity = MapToEntity(feedback);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<ArbitroValidationFeedbackEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<ArbitroValidationFeedbackEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task<(int total, int correct, int incorrect, int uncertain, double avgRating)> GetAccuracyMetricsAsync(
        Guid? gameSessionId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<ArbitroValidationFeedbackEntity>().AsNoTracking();

        if (gameSessionId.HasValue)
            query = query.Where(f => f.GameSessionId == gameSessionId.Value);

        if (since.HasValue)
            query = query.Where(f => f.SubmittedAt >= since.Value);

        var feedbacks = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (feedbacks.Count == 0)
            return (0, 0, 0, 0, 0.0);

        var total = feedbacks.Count;
        var correct = feedbacks.Count(f => string.Equals(f.Accuracy, "Correct", StringComparison.Ordinal));
        var incorrect = feedbacks.Count(f => string.Equals(f.Accuracy, "Incorrect", StringComparison.Ordinal));
        var uncertain = feedbacks.Count(f => string.Equals(f.Accuracy, "Uncertain", StringComparison.Ordinal));
        var avgRating = feedbacks.Average(f => f.Rating);

        return (total, correct, incorrect, uncertain, avgRating);
    }

    public async Task<IReadOnlyList<ArbitroValidationFeedback>> GetConflictFeedbackAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ArbitroValidationFeedbackEntity>()
            .AsNoTracking()
            .Where(f => f.HadConflicts)
            .OrderByDescending(f => f.SubmittedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    // ========== Mapping Methods ==========

    private static ArbitroValidationFeedback MapToDomain(ArbitroValidationFeedbackEntity entity)
    {
        var accuracy = Enum.Parse<AccuracyAssessment>(entity.Accuracy, ignoreCase: true);

        var feedback = ArbitroValidationFeedback.Create(
            validationId: entity.ValidationId,
            gameSessionId: entity.GameSessionId,
            userId: entity.UserId,
            rating: entity.Rating,
            accuracy: accuracy,
            aiDecision: entity.AiDecision,
            aiConfidence: entity.AiConfidence,
            hadConflicts: entity.HadConflicts,
            comment: entity.Comment);

        // Override the generated ID with the one from persistence
        var idProp = typeof(ArbitroValidationFeedback).GetProperty("Id");
        idProp?.SetValue(feedback, entity.Id);

        // Override timestamp from DB
        var submittedAtProp = typeof(ArbitroValidationFeedback).GetProperty("SubmittedAt");
        submittedAtProp?.SetValue(feedback, entity.SubmittedAt);

        return feedback;
    }

    private static ArbitroValidationFeedbackEntity MapToEntity(ArbitroValidationFeedback feedback)
    {
        return new ArbitroValidationFeedbackEntity
        {
            Id = feedback.Id,
            ValidationId = feedback.ValidationId,
            GameSessionId = feedback.GameSessionId,
            UserId = feedback.UserId,
            Rating = feedback.Rating,
            Accuracy = feedback.Accuracy.ToString(),
            Comment = feedback.Comment,
            AiDecision = feedback.AiDecision,
            AiConfidence = feedback.AiConfidence,
            HadConflicts = feedback.HadConflicts,
            SubmittedAt = feedback.SubmittedAt
        };
    }
}
