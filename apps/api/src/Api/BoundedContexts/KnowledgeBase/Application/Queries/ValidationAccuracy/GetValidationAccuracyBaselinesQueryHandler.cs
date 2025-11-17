using System.Text.Json;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Abstractions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ValidationAccuracy;

/// <summary>
/// Handler for GetValidationAccuracyBaselinesQuery.
/// BGAI-039: Retrieves validation accuracy baselines with optional filtering.
/// </summary>
public class GetValidationAccuracyBaselinesQueryHandler
    : IQueryHandler<GetValidationAccuracyBaselinesQuery, ErrorOr<List<ValidationAccuracyBaselineDto>>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetValidationAccuracyBaselinesQueryHandler> _logger;

    public GetValidationAccuracyBaselinesQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetValidationAccuracyBaselinesQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ErrorOr<List<ValidationAccuracyBaselineDto>>> Handle(
        GetValidationAccuracyBaselinesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving validation accuracy baselines with filters: Context={Context}, DatasetId={DatasetId}, MeetsBaselineOnly={MeetsBaselineOnly}, Limit={Limit}",
            request.Context, request.DatasetId, request.MeetsBaselineOnly, request.Limit);

        // Build query with filters
        var query = _dbContext.ValidationAccuracyBaselines.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            query = query.Where(b => b.Context == request.Context);
        }

        if (!string.IsNullOrWhiteSpace(request.DatasetId))
        {
            query = query.Where(b => b.DatasetId == request.DatasetId);
        }

        if (request.MeetsBaselineOnly.HasValue && request.MeetsBaselineOnly.Value)
        {
            query = query.Where(b => b.MeetsBaseline);
        }

        // Order by most recent first, apply limit
        var entities = await query
            .OrderByDescending(b => b.MeasuredAt)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} validation accuracy baselines", entities.Count);

        // Map to DTOs
        var results = entities.Select(e =>
        {
            List<string> recommendations;
            try
            {
                recommendations = string.IsNullOrEmpty(e.RecommendationsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(e.RecommendationsJson) ?? new List<string>();
            }
            catch
            {
                recommendations = new List<string>();
            }

            return new ValidationAccuracyBaselineDto
            {
                Id = e.Id,
                Context = e.Context,
                DatasetId = e.DatasetId,
                EvaluationId = e.EvaluationId,
                MeasuredAt = e.MeasuredAt,
                TruePositives = e.TruePositives,
                TrueNegatives = e.TrueNegatives,
                FalsePositives = e.FalsePositives,
                FalseNegatives = e.FalseNegatives,
                TotalCases = e.TotalCases,
                Precision = e.Precision,
                Recall = e.Recall,
                F1Score = e.F1Score,
                Accuracy = e.Accuracy,
                Specificity = e.Specificity,
                MatthewsCorrelation = e.MatthewsCorrelation,
                MeetsBaseline = e.MeetsBaseline,
                QualityLevel = ((BoundedContexts.KnowledgeBase.Domain.ValueObjects.ValidationAccuracyLevel)e.QualityLevel).ToString(),
                Summary = e.Summary,
                Recommendations = recommendations
            };
        }).ToList();

        return results;
    }
}
