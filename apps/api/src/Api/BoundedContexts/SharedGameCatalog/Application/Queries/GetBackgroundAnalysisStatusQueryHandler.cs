using System.Text.Json;
using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.BackgroundTasks;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for retrieving background analysis status.
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
internal sealed class GetBackgroundAnalysisStatusQueryHandler
    : IQueryHandler<GetBackgroundAnalysisStatusQuery, BackgroundAnalysisStatusDto>
{
    private readonly IBackgroundTaskOrchestrator _taskOrchestrator;
    private readonly IRulebookAnalysisRepository _analysisRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetBackgroundAnalysisStatusQueryHandler> _logger;

    public GetBackgroundAnalysisStatusQueryHandler(
        IBackgroundTaskOrchestrator taskOrchestrator,
        IRulebookAnalysisRepository analysisRepository,
        IDistributedCache cache,
        ILogger<GetBackgroundAnalysisStatusQueryHandler> logger)
    {
        _taskOrchestrator = taskOrchestrator ?? throw new ArgumentNullException(nameof(taskOrchestrator));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackgroundAnalysisStatusDto> Handle(
        GetBackgroundAnalysisStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving status for background analysis: taskId={TaskId}",
            query.TaskId);

        // 1. Get task status from orchestrator
        var taskStatus = await _taskOrchestrator.GetStatusAsync(query.TaskId)
            .ConfigureAwait(false);

        if (taskStatus is null)
        {
            _logger.LogWarning("Background analysis task {TaskId} not found", query.TaskId);
            return BackgroundAnalysisStatusDto.CreateFailed(query.TaskId, "Task not found");
        }

        if (taskStatus == BackgroundTaskStatus.Failed)
        {
            _logger.LogWarning("Background analysis task {TaskId} failed", query.TaskId);
            return BackgroundAnalysisStatusDto.CreateFailed(query.TaskId, "Analysis failed");
        }

        // 2. Read progress from Redis
        var progressKey = RedisKeyConstants.GetProgressKey(query.TaskId);
        var progressJson = await _cache.GetStringAsync(progressKey, cancellationToken)
            .ConfigureAwait(false);

        int progressPercentage = 0;
        string? currentPhase = null;
        string? statusMessage = null;
        TimeSpan? estimatedTime = null;

        if (!string.IsNullOrEmpty(progressJson))
        {
            try
            {
                var progress = JsonSerializer.Deserialize<ProgressData>(progressJson);
                if (progress is not null)
                {
                    progressPercentage = progress.Percentage;
                    currentPhase = progress.Phase;
                    statusMessage = progress.Message;

                    // Parse estimated time if present
                    if (progress.EstimatedSeconds.HasValue)
                    {
                        estimatedTime = TimeSpan.FromSeconds(progress.EstimatedSeconds.Value);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize progress data for task {TaskId}", query.TaskId);
            }
        }

        // 3. If completed, retrieve analysis result from DB
        if (taskStatus == BackgroundTaskStatus.Completed)
        {
            var analysis = await _analysisRepository.GetActiveAnalysisAsync(
                query.SharedGameId,
                query.PdfDocumentId,
                cancellationToken).ConfigureAwait(false);

            if (analysis is not null)
            {
                var analysisDto = MapToDto(analysis);
                return BackgroundAnalysisStatusDto.CreateCompleted(query.TaskId, analysisDto);
            }

            _logger.LogWarning(
                "Task {TaskId} completed but no analysis found in DB for game {GameId}, PDF {PdfId}",
                query.TaskId, query.SharedGameId, query.PdfDocumentId);
        }

        // 4. Return running/scheduled status
        var publicStatus = MapToPublicStatus(taskStatus.Value);
        return BackgroundAnalysisStatusDto.CreateRunning(
            query.TaskId,
            publicStatus,
            progressPercentage,
            currentPhase,
            statusMessage,
            estimatedTime);
    }

    private static RulebookAnalysisDto MapToDto(Domain.Entities.RulebookAnalysis analysis)
    {
        var victoryConditionsDto = analysis.VictoryConditions is not null
            ? new VictoryConditionsDto(
                analysis.VictoryConditions.Primary,
                analysis.VictoryConditions.Alternatives.ToList(),
                analysis.VictoryConditions.IsPointBased,
                analysis.VictoryConditions.TargetPoints)
            : null;

        var resourceDtos = analysis.Resources
            .Select(r => new ResourceDto(r.Name, r.Type, r.Usage, r.IsLimited))
            .ToList();

        var phaseDtos = analysis.GamePhases
            .Select(p => new GamePhaseDto(p.Name, p.Description, p.Order, p.IsOptional))
            .ToList();

        return new RulebookAnalysisDto(
            analysis.Id,
            analysis.SharedGameId,
            analysis.PdfDocumentId,
            analysis.GameTitle,
            analysis.Summary,
            analysis.KeyMechanics.ToList(),
            victoryConditionsDto,
            resourceDtos,
            phaseDtos,
            analysis.CommonQuestions.ToList(),
            analysis.ConfidenceScore,
            analysis.Version,
            analysis.IsActive,
            analysis.Source,
            analysis.AnalyzedAt,
            analysis.CreatedBy);
    }

    private static AnalysisTaskStatus MapToPublicStatus(BackgroundTaskStatus internalStatus) => internalStatus switch
    {
        BackgroundTaskStatus.Scheduled => AnalysisTaskStatus.Scheduled,
        BackgroundTaskStatus.Running => AnalysisTaskStatus.Running,
        BackgroundTaskStatus.Completed => AnalysisTaskStatus.Completed,
        BackgroundTaskStatus.Failed => AnalysisTaskStatus.Failed,
        BackgroundTaskStatus.Cancelled => AnalysisTaskStatus.Cancelled,
        _ => AnalysisTaskStatus.Failed
    };

    private sealed record ProgressData(
        string? Phase,
        int Percentage,
        string? Message,
        double? EstimatedSeconds
    );
}
