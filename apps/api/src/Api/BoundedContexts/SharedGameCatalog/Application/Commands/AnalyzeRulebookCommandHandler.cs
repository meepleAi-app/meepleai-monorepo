using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundTasks;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for analyzing rulebooks using AI to extract structured game information.
/// Issue #2402: Rulebook Analysis Service
/// Issue #2453: Redis Caching Layer
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
internal sealed class AnalyzeRulebookCommandHandler
    : ICommandHandler<AnalyzeRulebookCommand, AnalyzeRulebookResultDto>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly IRulebookAnalysisRepository _analysisRepository;
    private readonly IRulebookAnalyzer _rulebookAnalyzer;
    private readonly IBackgroundRulebookAnalysisOrchestrator _backgroundOrchestrator;
    private readonly IBackgroundTaskOrchestrator _taskOrchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly BackgroundAnalysisOptions _options;
    private readonly ILogger<AnalyzeRulebookCommandHandler> _logger;

    public AnalyzeRulebookCommandHandler(
        ISharedGameRepository gameRepository,
        IRulebookAnalysisRepository analysisRepository,
        IRulebookAnalyzer rulebookAnalyzer,
        IBackgroundRulebookAnalysisOrchestrator backgroundOrchestrator,
        IBackgroundTaskOrchestrator taskOrchestrator,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        IOptions<BackgroundAnalysisOptions> options,
        ILogger<AnalyzeRulebookCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _rulebookAnalyzer = rulebookAnalyzer ?? throw new ArgumentNullException(nameof(rulebookAnalyzer));
        _backgroundOrchestrator = backgroundOrchestrator ?? throw new ArgumentNullException(nameof(backgroundOrchestrator));
        _taskOrchestrator = taskOrchestrator ?? throw new ArgumentNullException(nameof(taskOrchestrator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnalyzeRulebookResultDto> Handle(
        AnalyzeRulebookCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Analyzing rulebook for game {GameId}, PDF {PdfId}",
            command.SharedGameId,
            command.PdfDocumentId);

        // 1. Verify shared game exists
        var game = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // Issue #5443: Gate - only analyzable document categories enter the pipeline
        var categoryString = await _analysisRepository.GetPdfDocumentCategoryAsync(
            command.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(categoryString)
            && Enum.TryParse<DocumentCategory>(categoryString, ignoreCase: true, out var category)
            && !category.IsAnalyzable())
        {
            _logger.LogInformation(
                "PDF {PdfId} has category {Category} which is not analyzable. Skipping analysis.",
                command.PdfDocumentId, categoryString);

            throw new ConflictException(
                $"Document category '{categoryString}' is not eligible for rulebook analysis. " +
                $"Only Rulebook, Expansion, and Errata documents can be analyzed.");
        }

        // 2. Get PDF document text content
        var rulebookContent = await _analysisRepository.GetPdfTextAsync(
            command.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rulebookContent))
        {
            _logger.LogWarning(
                "PDF document {PdfId} has no extracted text. Analysis will use fallback.",
                command.PdfDocumentId);
        }

        // 3. Route to sync/async based on content complexity (Issue #5451)
        var complexity = ContentComplexityScore.ComputeFromText(rulebookContent);

        _logger.LogInformation(
            "Content complexity for PDF {PdfId}: {Complexity}",
            command.PdfDocumentId, complexity);

        if (complexity.Decision == RoutingDecision.Background)
        {
            _logger.LogInformation(
                "Complex rulebook detected (score={Score:F4} > 0.4), scheduling background analysis",
                complexity.Score);

            return await ScheduleBackgroundAnalysisAsync(
                command.SharedGameId,
                command.PdfDocumentId,
                game.Title,
                rulebookContent,
                command.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Simple rulebook (score={Score:F4} <= 0.4), using synchronous analysis",
            complexity.Score);

        // 3b. Synchronous analysis for small rulebooks (existing MVP flow)
        var analysisResult = await _rulebookAnalyzer.AnalyzeAsync(
            game.Title,
            rulebookContent,
            cancellationToken).ConfigureAwait(false);

        // 4. Determine version for new analysis
        var existingAnalyses = await _analysisRepository.GetByPdfDocumentIdAsync(
            command.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        var newVersion = DetermineNextVersion(existingAnalyses);

        // 5. Create new RulebookAnalysis entity
        var analysis = RulebookAnalysis.CreateFromAI(
            command.SharedGameId,
            command.PdfDocumentId,
            analysisResult.GameTitle,
            analysisResult.Summary,
            analysisResult.KeyMechanics,
            analysisResult.VictoryConditions,
            analysisResult.Resources,
            analysisResult.GamePhases,
            analysisResult.CommonQuestions,
            analysisResult.ConfidenceScore,
            command.UserId,
            newVersion);

        // 6. Deactivate all previous analyses for this game+PDF
        await _analysisRepository.DeactivateAllAsync(
            command.SharedGameId,
            command.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        // 7. Set new analysis as active
        analysis.SetAsActive();

        // 8. Persist new analysis
        await _analysisRepository.AddAsync(analysis, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully analyzed rulebook {PdfId} for game {GameId}. Version: {Version}, Confidence: {Confidence}",
            command.PdfDocumentId,
            command.SharedGameId,
            newVersion,
            analysisResult.ConfidenceScore);

        // 9. Invalidate cache for this game+PDF combination
        await InvalidateCacheAsync(command.SharedGameId, command.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        // 10. Map to DTO
        var analysisDto = MapToDto(analysis);

        return AnalyzeRulebookResultDto.CreateSynchronous(analysisDto);
    }

    /// <summary>
    /// Schedules background analysis for large rulebooks with distributed locking.
    /// Issue #2454: Background Processing for Large Rulebooks
    /// </summary>
    private async Task<AnalyzeRulebookResultDto> ScheduleBackgroundAnalysisAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameTitle,
        string rulebookContent,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var taskId = Guid.NewGuid().ToString();
        var taskName = $"rulebook-analysis-{gameTitle}";
        var lockKey = RedisKeyConstants.GetAnalysisLockKey(sharedGameId, pdfDocumentId);
        var lockTimeout = TimeSpan.FromMinutes(10);

        _logger.LogInformation(
            "Scheduling background analysis with lock: taskId={TaskId}, game={GameTitle}, lockKey={LockKey}",
            taskId, gameTitle, lockKey);

        // Execute scheduling inside distributed lock to prevent duplicate tasks
        var lockAcquired = await _taskOrchestrator.ExecuteWithDistributedLockAsync(
            lockKey,
            async ct =>
            {
                await _taskOrchestrator.ScheduleAsync(
                    taskId,
                    taskName,
                    async taskCt =>
                    {
                        // Issue #2527: Enforce 5-minute timeout for background analysis tasks
                        // Create linked CancellationTokenSource with timeout to prevent resource leaks
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(taskCt);
                        cts.CancelAfter(_options.MaxTaskTimeout);

                        try
                        {
                            var result = await _backgroundOrchestrator.ExecuteBackgroundAnalysisAsync(
                                taskId,
                                sharedGameId,
                                pdfDocumentId,
                                gameTitle,
                                rulebookContent,
                                userId,
                                cts.Token).ConfigureAwait(false);

                            if (!result.Success)
                            {
                                _logger.LogError(
                                    "Background analysis failed: taskId={TaskId}, error={Error}",
                                    taskId, result.ErrorMessage);
                                throw new ConflictException($"Background analysis failed: {result.ErrorMessage}");
                            }

                            _logger.LogInformation(
                                "Background analysis completed: taskId={TaskId}, duration={Duration}s",
                                taskId, result.Metrics.TotalDuration.TotalSeconds);
                        }
                        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
                        {
                            // Issue #2527: Log timeout event with structured metadata
                            _logger.LogError(ex,
                                "Background analysis timed out after {Timeout}: taskId={TaskId}, game={GameTitle}, " +
                                "sharedGameId={SharedGameId}, pdfId={PdfId}",
                                _options.MaxTaskTimeout, taskId, gameTitle, sharedGameId, pdfDocumentId);

                            // Re-throw as ConflictException to mark task as Failed (not Cancelled) in Redis
                            // The BackgroundRulebookAnalysisOrchestrator already updates progress with timeout message
                            throw new ConflictException(
                                $"Background analysis timed out after {_options.MaxTaskTimeout}. " +
                                $"Task terminated to prevent resource leaks.", ex);
                        }
                    },
                    ct).ConfigureAwait(false);
            },
            lockTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!lockAcquired)
        {
            _logger.LogWarning(
                "Analysis already in progress for game {GameId}, PDF {PdfId}",
                sharedGameId, pdfDocumentId);

            throw new InvalidOperationException(
                $"Rulebook analysis already in progress. Please wait for completion.");
        }

        return AnalyzeRulebookResultDto.CreateBackgroundTask(taskId);
    }

    /// <summary>
    /// Invalidates cached analysis for a specific game and PDF document.
    /// Issue #2453: Redis Caching Layer
    /// </summary>
    private async Task InvalidateCacheAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var gameTag = RedisKeyConstants.GetGameTag(sharedGameId);
            await _cache.RemoveByTagAsync(gameTag, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Cache invalidated for game {GameId}, PDF {PdfId}",
                sharedGameId,
                pdfDocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to invalidate cache for game {GameId}, PDF {PdfId}. Cache may serve stale data temporarily.",
                sharedGameId,
                pdfDocumentId);
            // Non-fatal: Continue even if cache invalidation fails
        }
    }

    /// <summary>
    /// Determines the next version number based on existing analyses.
    /// </summary>
    private static string DetermineNextVersion(List<RulebookAnalysis> existingAnalyses)
    {
        if (existingAnalyses.Count == 0)
        {
            return "1.0";
        }

        // Find the highest version
        var versions = existingAnalyses
            .Select(a => a.Version.Split('.'))
            .Where(parts => parts.Length == 2
                && int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(parts => (Major: int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                             Minor: int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        if (versions.Count == 0)
        {
            return "1.0";
        }

        var maxVersion = versions.Max();
        return $"{maxVersion.Major}.{maxVersion.Minor + 1}";
    }

    /// <summary>
    /// Maps RulebookAnalysis entity to DTO.
    /// </summary>
    private static RulebookAnalysisDto MapToDto(RulebookAnalysis analysis)
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
}
