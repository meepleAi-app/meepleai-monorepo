using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Default <see cref="IMechanicAnalysisExecutor"/>. Loads the <c>Draft</c> aggregate, runs the
/// six-section LLM pipeline, parses the per-section JSON envelopes into
/// <see cref="MechanicClaim"/>+<see cref="MechanicCitation"/> children, persists the
/// <c>mechanic_analysis_section_runs</c> rows, and transitions the aggregate to
/// <c>InReview</c> on success or auto-rejects it on pipeline abort.
/// </summary>
/// <remarks>
/// The executor is intentionally scoped per background run — the command handler passes it the
/// analysis id after creating a fresh <see cref="IServiceScope"/> via
/// <see cref="IServiceScopeFactory"/>. All writes (section runs + aggregate state + audit rows
/// synthesised by the repository) commit atomically via a single
/// <see cref="IUnitOfWork.SaveChangesAsync(CancellationToken)"/>.
///
/// Idempotency: if the aggregate is not in <see cref="MechanicAnalysisStatus.Draft"/> when the
/// executor fires (e.g., a prior run already moved it to InReview / Rejected), the method exits
/// without touching state. This covers retries triggered by
/// <see cref="Api.Services.IBackgroundTaskService"/> as well as concurrent operator actions.
/// </remarks>
internal sealed class MechanicAnalysisExecutor : IMechanicAnalysisExecutor
{
    // Mirrors the handler's budget. Kept in sync because the context bundle is the same shape
    // shipped to every section in M1.2 (M1.3 introduces per-section semantic retrieval).
    private const int MaxRetrievedCharacters = 240_000;

    // DeepSeek list pricing as of 2026-04 (see memory/project_deepseek_llm.md). Used only when
    // the LLM result does not attribute its own cost; the pipeline's RecordUsage below prefers
    // the real spend coming back from the LlmCompletionResult.
    private const decimal DefaultInputCostPerMillion = 0.14m;
    private const decimal DefaultOutputCostPerMillion = 0.28m;

    // The six mechanic sections covered by M1.2. The pipeline fans out one LLM call per section.
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
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMechanicAnalysisPipeline _pipeline;
    private readonly IMechanicPromptProvider _promptProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MechanicAnalysisExecutor> _logger;

    public MechanicAnalysisExecutor(
        IMechanicAnalysisRepository analysisRepository,
        MeepleAiDbContext dbContext,
        IMechanicAnalysisPipeline pipeline,
        IMechanicPromptProvider promptProvider,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<MechanicAnalysisExecutor> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        var analysis = await _analysisRepository
            .GetByIdWithClaimsAsync(analysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            _logger.LogWarning(
                "Mechanic analysis {AnalysisId} not found when background executor fired; skipping.",
                analysisId);
            return;
        }

        if (analysis.Status != MechanicAnalysisStatus.Draft)
        {
            _logger.LogInformation(
                "Mechanic analysis {AnalysisId} is in status {Status}; executor exits idempotently.",
                analysisId, analysis.Status);
            return;
        }

        try
        {
            await RunPipelineAsync(analysis, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // Cooperative cancellation: reload and auto-reject so the aggregate ends in a
            // terminal state rather than stuck in Draft. We swallow the exception on purpose
            // so the background task service sees a completed (not cancelled) run in its
            // telemetry and downstream notifications fire.
            _logger.LogWarning(
                ex,
                "Mechanic analysis {AnalysisId} execution cancelled; auto-rejecting to terminal state.",
                analysisId);
            await HandleCancellationAsync(analysisId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Mechanic analysis {AnalysisId} pipeline crashed unexpectedly; auto-rejecting.",
                analysisId);
            await HandleUnexpectedFailureAsync(analysisId, ex).ConfigureAwait(false);
        }
    }

    private async Task RunPipelineAsync(MechanicAnalysis analysis, CancellationToken cancellationToken)
    {
        var retrievalContext = await LoadRetrievalContextAsync(analysis.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(retrievalContext))
        {
            _logger.LogWarning(
                "Mechanic analysis {AnalysisId} aborted: PDF {PdfId} has no indexed chunks at execution time.",
                analysis.Id, analysis.PdfDocumentId);

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            analysis.AutoRejectFromDraft(
                MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed,
                analysis.CreatedBy,
                utcNow);
            _analysisRepository.Update(analysis);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        // M1.2 ships the same rulebook bundle to every section. M1.3 will swap this for per-
        // section semantic retrieval.
        var contextBySection = AllSections.ToDictionary(s => s, _ => retrievalContext);

        var request = new MechanicPipelineRequest(
            AnalysisId: analysis.Id,
            SharedGameId: analysis.SharedGameId,
            PdfDocumentId: analysis.PdfDocumentId,
            PromptVersion: _promptProvider.PromptVersion,
            Sections: AllSections,
            RetrievedContextBySection: contextBySection,
            Provider: analysis.Provider,
            Model: analysis.ModelUsed,
            EffectiveCostCapUsd: analysis.CostCapUsd,
            InputCostPerMillionTokens: DefaultInputCostPerMillion,
            OutputCostPerMillionTokens: DefaultOutputCostPerMillion);

        var result = await _pipeline.RunAsync(request, cancellationToken).ConfigureAwait(false);

        // Persist section-run telemetry first. We do this regardless of outcome so operators
        // always have a trace row per section attempt (T6 auditability).
        if (result.SectionRuns.Count > 0)
        {
            await _dbContext.MechanicAnalysisSectionRuns
                .AddRangeAsync(result.SectionRuns, cancellationToken)
                .ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (result.Outcome == MechanicPipelineOutcome.Succeeded)
        {
            await ApplySuccessAsync(analysis, result, now).ConfigureAwait(false);
        }
        else
        {
            ApplyAbort(analysis, result, now);
        }

        _analysisRepository.Update(analysis);

        // Use CancellationToken.None for the final commit: if the caller cancels after the LLM
        // has already run we still want the aggregate state (and section-run audit rows) to
        // reach disk so we don't lose the spend record.
        await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        _logger.LogInformation(
            "Mechanic analysis {AnalysisId} finished: outcome={Outcome}, claims={Claims}, tokens={Tokens}, cost={Cost:F4} USD.",
            analysis.Id, result.Outcome, analysis.Claims.Count, result.TotalPromptTokens + result.TotalCompletionTokens, result.TotalCostUsd);
    }

    private async Task ApplySuccessAsync(MechanicAnalysis analysis, MechanicPipelineResult result, DateTime utcNow)
    {
        var parsed = MechanicOutputParser.Parse(analysis.Id, result.SectionOutputs);

        if (parsed.Count == 0)
        {
            _logger.LogWarning(
                "Mechanic analysis {AnalysisId} pipeline succeeded but parser produced no claims; auto-rejecting.",
                analysis.Id);
            analysis.RecordUsage(
                result.TotalPromptTokens + result.TotalCompletionTokens,
                result.TotalCostUsd);
            analysis.AutoRejectFromDraft(
                MechanicAnalysis.AutoRejectionReasons.ValidationFailedBeyondRetry,
                analysis.CreatedBy,
                utcNow);
            return;
        }

        foreach (var claim in parsed)
        {
            analysis.AddClaim(claim);
        }

        analysis.RecordUsage(
            result.TotalPromptTokens + result.TotalCompletionTokens,
            result.TotalCostUsd);

        // Transition Draft → InReview. CreatedBy is used as the actor because M1.2 treats the
        // AI author as the submitter; human review starts in the subsequent phase.
        analysis.SubmitForReview(analysis.CreatedBy, utcNow);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static void ApplyAbort(MechanicAnalysis analysis, MechanicPipelineResult result, DateTime utcNow)
    {
        var reason = result.Outcome switch
        {
            MechanicPipelineOutcome.AbortedCostCap => MechanicAnalysis.AutoRejectionReasons.CostCapExceeded,
            MechanicPipelineOutcome.AbortedValidation => MechanicAnalysis.AutoRejectionReasons.ValidationFailedBeyondRetry,
            MechanicPipelineOutcome.AbortedLlmFailed => MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed,
            _ => MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed
        };

        analysis.RecordUsage(
            result.TotalPromptTokens + result.TotalCompletionTokens,
            result.TotalCostUsd);
        analysis.AutoRejectFromDraft(reason, analysis.CreatedBy, utcNow);
    }

    private async Task HandleCancellationAsync(Guid analysisId)
    {
        try
        {
            // Clear any entities the pipeline may have staged on this scoped DbContext before the
            // cancellation fired (e.g. partial section runs added mid-pipeline). The cancellation
            // commit MUST only persist the aggregate's Rejected transition + its audit row — not
            // partial telemetry the pipeline hadn't yet decided to keep. The fresh detached
            // reload below (AsNoTracking via the repository) plus Update() reattaches only the
            // aggregate root.
            _dbContext.ChangeTracker.Clear();

            var analysis = await _analysisRepository
                .GetByIdWithClaimsAsync(analysisId, CancellationToken.None)
                .ConfigureAwait(false);

            if (analysis is null || analysis.Status != MechanicAnalysisStatus.Draft)
            {
                return;
            }

            analysis.AutoRejectFromDraft(
                MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed,
                analysis.CreatedBy,
                _timeProvider.GetUtcNow().UtcDateTime);
            _analysisRepository.Update(analysis);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Mechanic analysis {AnalysisId} could not be auto-rejected after cancellation.",
                analysisId);
        }
    }

    private async Task HandleUnexpectedFailureAsync(Guid analysisId, Exception originalException)
    {
        try
        {
            // Same rationale as HandleCancellationAsync: a pipeline crash may leave half-staged
            // entities on the scoped DbContext. Clear before the rejection commit so we only
            // persist the terminal aggregate transition + its audit row.
            _dbContext.ChangeTracker.Clear();

            var analysis = await _analysisRepository
                .GetByIdWithClaimsAsync(analysisId, CancellationToken.None)
                .ConfigureAwait(false);

            if (analysis is null || analysis.Status != MechanicAnalysisStatus.Draft)
            {
                return;
            }

            analysis.AutoRejectFromDraft(
                MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed,
                analysis.CreatedBy,
                _timeProvider.GetUtcNow().UtcDateTime);
            _analysisRepository.Update(analysis);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogError(
                cleanupEx,
                "Mechanic analysis {AnalysisId} failed to transition to Rejected after pipeline crash. " +
                "Original exception: {OriginalMessage}",
                analysisId, originalException.Message);
        }
    }

    private async Task<string> LoadRetrievalContextAsync(Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        var chunks = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(c => c.PdfDocumentId == pdfDocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new { c.ChunkIndex, c.PageNumber, c.Content })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
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
}
