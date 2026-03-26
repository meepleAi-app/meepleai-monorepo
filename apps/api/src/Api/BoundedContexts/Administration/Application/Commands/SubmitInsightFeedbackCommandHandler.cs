using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.Middleware.Exceptions;
using Api.Observability;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SubmitInsightFeedbackCommand.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// Persists user feedback on AI insights and records Prometheus metrics.
/// </summary>
internal sealed class SubmitInsightFeedbackCommandHandler : IRequestHandler<SubmitInsightFeedbackCommand, Guid>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SubmitInsightFeedbackCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public SubmitInsightFeedbackCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<SubmitInsightFeedbackCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Guid> Handle(SubmitInsightFeedbackCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check for duplicate feedback (unique constraint: UserId + InsightId)
        var existingFeedback = await _dbContext.Set<InsightFeedbackEntity>()
            .AsNoTracking()
            .AnyAsync(
                f => f.UserId == request.UserId && f.InsightId == request.InsightId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingFeedback)
        {
            throw new ConflictException($"Feedback for insight '{request.InsightId}' already submitted by this user");
        }

        var entity = new InsightFeedbackEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            InsightId = request.InsightId,
            InsightType = request.InsightType,
            IsRelevant = request.IsRelevant,
            Comment = request.Comment?.Trim(),
            SubmittedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _dbContext.Set<InsightFeedbackEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Record Prometheus metrics for accuracy tracking
        MeepleAiMetrics.RecordInsightFeedback(request.InsightType, request.IsRelevant);

        _logger.LogInformation(
            "InsightFeedback submitted: id={FeedbackId}, insightId={InsightId}, type={InsightType}, relevant={IsRelevant}",
            entity.Id, request.InsightId, request.InsightType, request.IsRelevant);

        return entity.Id;
    }
}
