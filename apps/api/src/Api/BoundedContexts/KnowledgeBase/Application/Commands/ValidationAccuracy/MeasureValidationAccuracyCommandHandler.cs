using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Abstractions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.ValidationAccuracy;

/// <summary>
/// Handler for MeasureValidationAccuracyCommand.
/// BGAI-039: Calculates validation accuracy metrics from evaluation results.
/// </summary>
public class MeasureValidationAccuracyCommandHandler
    : ICommandHandler<MeasureValidationAccuracyCommand, ErrorOr<ValidationAccuracyBaselineDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ValidationAccuracyTrackingService _trackingService;
    private readonly ILogger<MeasureValidationAccuracyCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public MeasureValidationAccuracyCommandHandler(
        MeepleAiDbContext dbContext,
        ValidationAccuracyTrackingService trackingService,
        ILogger<MeasureValidationAccuracyCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _trackingService = trackingService ?? throw new ArgumentNullException(nameof(trackingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ErrorOr<ValidationAccuracyBaselineDto>> Handle(
        MeasureValidationAccuracyCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Measuring validation accuracy for context '{Context}', evaluation {EvaluationId}, expected valid count {ExpectedValid}",
            request.Context, request.EvaluationId, request.ExpectedValidCount);

        // Step 1: Fetch evaluation result from database
        var evaluationEntity = await _dbContext.PromptEvaluationResults
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EvaluationId, cancellationToken);

        if (evaluationEntity == null)
        {
            _logger.LogWarning("Evaluation result {EvaluationId} not found", request.EvaluationId);
            return Error.NotFound("EvaluationNotFound", $"Evaluation result {request.EvaluationId} not found");
        }

        // Step 2: Deserialize query results
        List<QueryEvaluationResult> queryResults;
        try
        {
            queryResults = string.IsNullOrEmpty(evaluationEntity.QueryResultsJson)
                ? new List<QueryEvaluationResult>()
                : JsonSerializer.Deserialize<List<QueryEvaluationResult>>(evaluationEntity.QueryResultsJson)
                  ?? new List<QueryEvaluationResult>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize query results for evaluation {EvaluationId}", request.EvaluationId);
            return Error.Failure("DeserializationFailed", "Failed to deserialize evaluation query results");
        }

        // Step 3: Build PromptEvaluationResult DTO from entity
        var evaluationResult = new PromptEvaluationResult
        {
            EvaluationId = evaluationEntity.Id.ToString(),
            TemplateId = evaluationEntity.TemplateId.ToString(),
            VersionId = evaluationEntity.VersionId.ToString(),
            DatasetId = evaluationEntity.DatasetId,
            ExecutedAt = evaluationEntity.ExecutedAt,
            TotalQueries = evaluationEntity.TotalQueries,
            Metrics = new EvaluationMetrics
            {
                Accuracy = evaluationEntity.Accuracy,
                Relevance = evaluationEntity.Relevance,
                Completeness = evaluationEntity.Completeness,
                Clarity = evaluationEntity.Clarity,
                CitationQuality = evaluationEntity.CitationQuality
            },
            Passed = evaluationEntity.Passed,
            Summary = evaluationEntity.Summary,
            QueryResults = queryResults
        };

        // Step 4: Calculate accuracy metrics
        var metrics = _trackingService.CalculateAccuracyMetrics(
            evaluationResult,
            request.ExpectedValidCount);

        // Step 5: Generate accuracy report
        var report = _trackingService.GenerateAccuracyReport(metrics, request.Context);

        _logger.LogInformation(
            "Validation accuracy calculated: {Accuracy:P2}, F1: {F1:P2}, Level: {Level}, Meets baseline: {Meets}",
            metrics.Accuracy, metrics.F1Score, report.QualityLevel, report.MeetsBaseline);

        // Step 6: Store baseline if requested
        Guid baselineId = Guid.NewGuid();
        if (request.StoreBaseline)
        {
            var baselineEntity = new ValidationAccuracyBaselineEntity
            {
                Id = baselineId,
                Context = request.Context,
                DatasetId = request.DatasetId,
                EvaluationId = request.EvaluationId,
                MeasuredAt = _timeProvider.GetUtcNow().UtcDateTime,
                TruePositives = metrics.TruePositives,
                TrueNegatives = metrics.TrueNegatives,
                FalsePositives = metrics.FalsePositives,
                FalseNegatives = metrics.FalseNegatives,
                TotalCases = metrics.Total,
                Precision = metrics.Precision,
                Recall = metrics.Recall,
                F1Score = metrics.F1Score,
                Accuracy = metrics.Accuracy,
                Specificity = metrics.Specificity,
                MatthewsCorrelation = metrics.MatthewsCorrelationCoefficient,
                MeetsBaseline = metrics.MeetsBaselineThreshold,
                QualityLevel = (int)metrics.QualityLevel,
                Summary = report.Summary,
                RecommendationsJson = JsonSerializer.Serialize(report.Recommendations),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _dbContext.ValidationAccuracyBaselines.Add(baselineEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Validation accuracy baseline {BaselineId} stored successfully", baselineId);
        }

        // Step 7: Return DTO
        return new ValidationAccuracyBaselineDto
        {
            Id = baselineId,
            Context = request.Context,
            DatasetId = request.DatasetId,
            EvaluationId = request.EvaluationId,
            MeasuredAt = _timeProvider.GetUtcNow().UtcDateTime,
            TruePositives = metrics.TruePositives,
            TrueNegatives = metrics.TrueNegatives,
            FalsePositives = metrics.FalsePositives,
            FalseNegatives = metrics.FalseNegatives,
            TotalCases = metrics.Total,
            Precision = metrics.Precision,
            Recall = metrics.Recall,
            F1Score = metrics.F1Score,
            Accuracy = metrics.Accuracy,
            Specificity = metrics.Specificity,
            MatthewsCorrelation = metrics.MatthewsCorrelationCoefficient,
            MeetsBaseline = metrics.MeetsBaselineThreshold,
            QualityLevel = metrics.QualityLevel.ToString(),
            Summary = report.Summary,
            Recommendations = report.Recommendations
        };
    }
}
