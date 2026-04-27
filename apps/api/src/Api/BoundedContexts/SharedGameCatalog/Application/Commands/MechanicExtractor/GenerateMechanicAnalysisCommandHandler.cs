using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="GenerateMechanicAnalysisCommand"/> (ISSUE-524 / M1.2, ADR-051).
/// </summary>
/// <remarks>
/// Synchronous portion (blocks the HTTP request):
/// <list type="number">
/// <item><description>Verify <c>SharedGame</c> exists → <see cref="NotFoundException"/> otherwise.</description></item>
/// <item><description>Verify the PDF is linked to the game via <c>shared_game_documents</c> → <see cref="NotFoundException"/> otherwise.</description></item>
/// <item><description>T7 idempotency: if a non-<c>Rejected</c> analysis already exists for (SharedGameId, PdfDocumentId, PromptVersion), short-circuit and return its state with <c>IsExistingAnalysis=true</c>.</description></item>
/// <item><description>Project cost via <see cref="IAnalysisCostEstimator"/>. If the projection exceeds the submitted cap and no override is provided, throw <see cref="ConflictException"/> (HTTP 409).</description></item>
/// <item><description>Create the aggregate in <c>Draft</c>, optionally applying <c>OverrideCostCap</c> for auditable cap raises (B3=A), and persist via <c>IUnitOfWork.SaveChangesAsync</c>.</description></item>
/// </list>
/// Asynchronous portion (fire-and-forget): enqueue the pipeline run via
/// <see cref="IBackgroundTaskService.ExecuteWithCancellation"/>. The background job owns its own
/// DI scope (via <see cref="IServiceScopeFactory"/>) so this handler's scope can exit cleanly.
/// The endpoint returns <c>202 Accepted</c> with <c>StatusUrl</c>.
/// </remarks>
internal sealed class GenerateMechanicAnalysisCommandHandler
    : ICommandHandler<GenerateMechanicAnalysisCommand, MechanicAnalysisGenerationResponseDto>
{
    // B4=A: prompts v1 target DeepSeek. These defaults align with ADR-007 routing.
    private const string DefaultProvider = "DeepSeek";
    private const string DefaultModel = "deepseek-chat";

    // Conservative DeepSeek list pricing as of 2026-04 (project_deepseek_llm memory).
    // Runtime cost is captured per-section from the actual LlmCompletionResult, so any drift
    // affects only the planning estimate — the hard cap still bites based on real spend.
    private const decimal DefaultInputCostPerMillion = 0.14m;
    private const decimal DefaultOutputCostPerMillion = 0.28m;

    // Hard ceiling on retrieved context shipped into the pipeline. DeepSeek-chat tolerates
    // ~64K context; we stay under that with room for prompt + completion. Conversion: ~4
    // chars per token — good enough for a planning budget.
    private const int MaxRetrievedCharacters = 240_000;

    // Section fan-out for M1.2 (all six sections generated in one run).
    private static readonly IReadOnlyList<MechanicSection> AllSections = new[]
    {
        MechanicSection.Summary,
        MechanicSection.Mechanics,
        MechanicSection.Victory,
        MechanicSection.Resources,
        MechanicSection.Phases,
        MechanicSection.Faq
    };

    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;

    // Read-side-only DbContext access for TextChunks (DocumentProcessing BC). We intentionally do
    // not introduce a cross-BC repository for what is a projection used exclusively to assemble
    // prompt input. All writes go through repositories + IUnitOfWork.
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMechanicPromptProvider _promptProvider;
    private readonly IAnalysisCostEstimator _costEstimator;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateMechanicAnalysisCommandHandler> _logger;

    public GenerateMechanicAnalysisCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        ISharedGameRepository sharedGameRepository,
        ISharedGameDocumentRepository documentRepository,
        MeepleAiDbContext dbContext,
        IMechanicPromptProvider promptProvider,
        IAnalysisCostEstimator costEstimator,
        IBackgroundTaskService backgroundTaskService,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        ILogger<GenerateMechanicAnalysisCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _costEstimator = costEstimator ?? throw new ArgumentNullException(nameof(costEstimator));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicAnalysisGenerationResponseDto> Handle(
        GenerateMechanicAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promptVersion = _promptProvider.PromptVersion;

        // 1. SharedGame existence (404 if missing).
        var sharedGame = await _sharedGameRepository
            .GetByIdAsync(request.SharedGameId, cancellationToken)
            .ConfigureAwait(false);
        if (sharedGame is null)
        {
            throw new NotFoundException(
                resourceType: "SharedGame",
                resourceId: request.SharedGameId.ToString());
        }

        // 2. PDF linkage (404 if the PDF is not associated with the game). Goes through the
        //    domain repository so the handler stays decoupled from EF entity projections.
        var pdfLinked = await _documentRepository
            .IsPdfLinkedToGameAsync(request.SharedGameId, request.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (!pdfLinked)
        {
            throw new NotFoundException(
                resourceType: "SharedGameDocument",
                resourceId: $"{request.SharedGameId}/{request.PdfDocumentId}");
        }

        // 3. T7 idempotency — return the existing non-rejected analysis unchanged.
        var existing = await _analysisRepository
            .FindByPromptVersionAsync(request.SharedGameId, request.PdfDocumentId, promptVersion, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Mechanic analysis idempotent short-circuit: existing {ExistingId} for (SharedGame={SharedGame}, Pdf={Pdf}, PromptVersion={Version}, Status={Status}).",
                existing.Id,
                request.SharedGameId,
                request.PdfDocumentId,
                promptVersion,
                existing.Status);

            return BuildResponse(
                existing.Id,
                existing.Status,
                promptVersion,
                effectiveCostCap: existing.CostCapUsd,
                estimatedCost: 0m,
                projectedTotalTokens: 0,
                costCapOverrideApplied: existing.CostCapOverrideBy is not null,
                isExisting: true);
        }

        // 4. Load retrieval context. M1.2 ships the same bundled rulebook content to every
        //    section (the section prompt itself narrows the focus). M1.3 will introduce a
        //    semantic retriever keyed on section intent.
        var retrievalContext = await LoadRetrievalContextAsync(request.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(retrievalContext))
        {
            throw new ConflictException(
                $"PDF {request.PdfDocumentId} has no indexed text chunks. Re-run extraction before generating a mechanic analysis.");
        }

        // 5. Cost projection (T8). Token count uses a 4-char/token heuristic consistent with
        //    the estimator's calibration in AnalysisCostEstimator.
        var retrievedPromptTokens = retrievalContext.Length / 4;
        var estimate = _costEstimator.Estimate(new AnalysisCostEstimateInput(
            PromptVersion: promptVersion,
            Provider: DefaultProvider,
            Model: DefaultModel,
            Sections: AllSections,
            TotalRetrievedPromptTokens: retrievedPromptTokens,
            InputCostPerMillionTokens: DefaultInputCostPerMillion,
            OutputCostPerMillionTokens: DefaultOutputCostPerMillion));

        var hasOverride = request.CostCapOverride is not null;
        var effectiveCostCap = hasOverride ? request.CostCapOverride!.NewCapUsd : request.CostCapUsd;

        // B2=A: hard cap with no overshoot. Projected cost must fit under the effective
        // cap up-front; planning rejects any analysis that would exceed it.
        if (estimate.ProjectedCostUsd > effectiveCostCap)
        {
            _logger.LogWarning(
                "Mechanic analysis rejected at planning: projected cost {Projected:F4} USD > cap {Cap:F4} USD (override={HasOverride}).",
                estimate.ProjectedCostUsd,
                effectiveCostCap,
                hasOverride);

            throw new ConflictException(
                $"Projected cost {estimate.ProjectedCostUsd:F4} USD exceeds the effective cap {effectiveCostCap:F4} USD. "
                + "Supply a CostCapOverride with justification or reduce the rulebook scope.");
        }

        // 6. Create aggregate in Draft, then optionally raise the cap for audit. The order
        //    matters: Create() enforces costCapUsd > 0; OverrideCostCap() requires the new
        //    cap to exceed the current one.
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var analysis = MechanicAnalysis.Create(
            sharedGameId: request.SharedGameId,
            pdfDocumentId: request.PdfDocumentId,
            promptVersion: promptVersion,
            createdBy: request.RequestedBy,
            createdAt: utcNow,
            modelUsed: DefaultModel,
            provider: DefaultProvider,
            costCapUsd: request.CostCapUsd);

        if (hasOverride)
        {
            analysis.OverrideCostCap(
                actorId: request.RequestedBy,
                newCapUsd: request.CostCapOverride!.NewCapUsd,
                reason: request.CostCapOverride.Reason,
                utcNow: utcNow);
        }

        await _analysisRepository.AddAsync(analysis, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Extremely narrow window: concurrent admin submitted the same (game, pdf, version).
            // Re-query and short-circuit to the winner.
            var winner = await _analysisRepository
                .FindByPromptVersionAsync(request.SharedGameId, request.PdfDocumentId, promptVersion, cancellationToken)
                .ConfigureAwait(false);

            if (winner is not null)
            {
                _logger.LogInformation(
                    ex,
                    "Mechanic analysis create lost race; returning concurrent winner {WinnerId}.", winner.Id);

                return BuildResponse(
                    winner.Id,
                    winner.Status,
                    promptVersion,
                    effectiveCostCap: winner.CostCapUsd,
                    estimatedCost: 0m,
                    projectedTotalTokens: 0,
                    costCapOverrideApplied: winner.CostCapOverrideBy is not null,
                    isExisting: true);
            }

            throw;
        }

        _logger.LogInformation(
            "Mechanic analysis {AnalysisId} queued for execution (SharedGame={SharedGame}, Pdf={Pdf}, ProjectedCost={Cost:F4}, EffectiveCap={Cap:F4}).",
            analysis.Id,
            request.SharedGameId,
            request.PdfDocumentId,
            estimate.ProjectedCostUsd,
            effectiveCostCap);

        // 7. Enqueue background execution. The callback must not capture the request-scoped
        //    services — it creates a fresh scope so the pipeline can run past the HTTP
        //    response.
        var analysisId = analysis.Id;
        _backgroundTaskService.ExecuteWithCancellation(
            taskId: BuildTaskId(analysisId),
            taskFactory: async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<IMechanicAnalysisExecutor>();
                await executor.ExecuteAsync(analysisId, ct).ConfigureAwait(false);
            });

        return BuildResponse(
            analysisId,
            analysis.Status,
            promptVersion,
            effectiveCostCap: effectiveCostCap,
            estimatedCost: estimate.ProjectedCostUsd,
            projectedTotalTokens: estimate.ProjectedTotalTokens,
            costCapOverrideApplied: hasOverride,
            isExisting: false);
    }

    private async Task<string> LoadRetrievalContextAsync(Guid pdfDocumentId, CancellationToken ct)
    {
        // Pull chunks in document order. Ordering matters for the LLM — we want a coherent
        // flow of the rulebook, not shuffled fragments. AsNoTracking() since we never mutate.
        var chunks = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(c => c.PdfDocumentId == pdfDocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new { c.ChunkIndex, c.PageNumber, c.Content })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        // Concatenate with page markers to preserve provenance. Budget-capped so the prompt
        // stays inside the model context window even for long rulebooks.
        var builder = new System.Text.StringBuilder();
        foreach (var chunk in chunks)
        {
            var header = chunk.PageNumber.HasValue
                ? $"[chunk #{chunk.ChunkIndex} · page {chunk.PageNumber}]"
                : $"[chunk #{chunk.ChunkIndex}]";

            if (builder.Length + header.Length + chunk.Content.Length + 8 > MaxRetrievedCharacters)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n---\n\n");
            }

            builder.Append(header).Append('\n').Append(chunk.Content);
        }

        return builder.ToString();
    }

    private static MechanicAnalysisGenerationResponseDto BuildResponse(
        Guid id,
        MechanicAnalysisStatus status,
        string promptVersion,
        decimal effectiveCostCap,
        decimal estimatedCost,
        int projectedTotalTokens,
        bool costCapOverrideApplied,
        bool isExisting) =>
        new(
            Id: id,
            Status: status,
            PromptVersion: promptVersion,
            CostCapUsd: effectiveCostCap,
            EstimatedCostUsd: estimatedCost,
            ProjectedTotalTokens: projectedTotalTokens,
            CostCapOverrideApplied: costCapOverrideApplied,
            StatusUrl: $"/api/v1/admin/mechanic-analyses/{id}/status",
            IsExistingAnalysis: isExisting);

    /// <summary>
    /// Task id used by <see cref="IBackgroundTaskService"/> for cancellation correlation.
    /// Stable per analysis so an operator can later invoke <c>CancelTask</c> if needed.
    /// </summary>
    internal static string BuildTaskId(Guid analysisId) => $"mechanic-analysis:{analysisId:D}";
}
