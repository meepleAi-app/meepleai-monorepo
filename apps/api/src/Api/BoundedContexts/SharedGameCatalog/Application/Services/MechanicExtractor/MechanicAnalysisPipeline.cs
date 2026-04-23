using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Default <see cref="IMechanicAnalysisPipeline"/>. Runs the per-section loop with:
/// prompt assembly → <see cref="ILlmService"/> call (JSON output) → validation →
/// cumulative cost check → <see cref="MechanicAnalysisSectionRunEntity"/> emission.
/// </summary>
/// <remarks>
/// Provider selection is delegated to <see cref="ILlmService"/>; the pipeline simply asks
/// for the requested model and records the metadata. In M1.3 we will switch to explicit
/// JSON schema strict mode via <c>ILlmClient</c> to reduce parse failures.
/// </remarks>
internal sealed class MechanicAnalysisPipeline : IMechanicAnalysisPipeline
{
    private const int MaxValidationRetries = 1;
    // NOTE: Temperature (target 0.2) and per-section MaxOutputTokens (target 1500) will be
    // threaded through via ILlmClient strict-mode in M1.3; the current ILlmService surface
    // does not expose these knobs.

    private readonly ILlmService _llmService;
    private readonly IMechanicPromptProvider _promptProvider;
    private readonly IMechanicOutputValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MechanicAnalysisPipeline> _logger;

    public MechanicAnalysisPipeline(
        ILlmService llmService,
        IMechanicPromptProvider promptProvider,
        IMechanicOutputValidator validator,
        TimeProvider timeProvider,
        ILogger<MechanicAnalysisPipeline> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicPipelineResult> RunAsync(MechanicPipelineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sectionRuns = new List<MechanicAnalysisSectionRunEntity>(request.Sections.Count);
        var outputs = new Dictionary<MechanicSection, string>(request.Sections.Count);
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;
        decimal totalCostUsd = 0m;
        var runOrder = 0;

        var systemPrompt = _promptProvider.GetSystemPrompt();

        foreach (var section in request.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sectionPrompt = _promptProvider.GetSectionPrompt(section);
            var context = request.RetrievedContextBySection.TryGetValue(section, out var ctx) ? ctx : string.Empty;
            var userPrompt = BuildUserPrompt(sectionPrompt, context);

            var (sectionRun, sectionOutput, sectionAbort) = await RunSectionAsync(
                request,
                section,
                runOrder,
                systemPrompt,
                userPrompt,
                cancellationToken).ConfigureAwait(false);

            runOrder++;
            sectionRuns.Add(sectionRun);

            totalPromptTokens += sectionRun.PromptTokens;
            totalCompletionTokens += sectionRun.CompletionTokens;
            totalCostUsd += sectionRun.EstimatedCostUsd;

            if (sectionAbort is not null)
            {
                return BuildAbortResult(sectionAbort.Value, sectionRun.ErrorMessage, sectionRuns, outputs,
                    totalPromptTokens, totalCompletionTokens, totalCostUsd);
            }

            if (totalCostUsd > request.EffectiveCostCapUsd)
            {
                _logger.LogWarning(
                    "Mechanic pipeline {AnalysisId} aborted at section {Section}: cumulative cost {Cost:F4} > cap {Cap:F4}.",
                    request.AnalysisId, section, totalCostUsd, request.EffectiveCostCapUsd);

                return BuildAbortResult(
                    MechanicPipelineOutcome.AbortedCostCap,
                    $"Cumulative cost {totalCostUsd:F6} USD exceeded effective cap {request.EffectiveCostCapUsd:F6} USD after section '{section}'.",
                    sectionRuns, outputs, totalPromptTokens, totalCompletionTokens, totalCostUsd);
            }

            if (sectionOutput is not null)
            {
                outputs[section] = sectionOutput;
            }
        }

        return new MechanicPipelineResult(
            Outcome: MechanicPipelineOutcome.Succeeded,
            SectionRuns: sectionRuns,
            SectionOutputs: outputs,
            TotalPromptTokens: totalPromptTokens,
            TotalCompletionTokens: totalCompletionTokens,
            TotalCostUsd: decimal.Round(totalCostUsd, 6, MidpointRounding.AwayFromZero),
            AbortDetail: null);
    }

    private async Task<(MechanicAnalysisSectionRunEntity Run, string? Output, MechanicPipelineOutcome? Abort)>
        RunSectionAsync(
            MechanicPipelineRequest request,
            MechanicSection section,
            int runOrder,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var stopwatch = Stopwatch.StartNew();
        string? lastValidationError = null;
        var attempts = MaxValidationRetries + 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _llmService.GenerateCompletionWithModelAsync(
                explicitModel: request.Model,
                systemPrompt: systemPrompt,
                userPrompt: userPrompt,
                source: RequestSource.Manual,
                ct: cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                stopwatch.Stop();
                var completedAt = _timeProvider.GetUtcNow().UtcDateTime;
                return (BuildFailedRun(
                    request,
                    section,
                    runOrder,
                    result,
                    startedAt,
                    completedAt,
                    stopwatch.ElapsedMilliseconds,
                    sectionStatus: 1,
                    errorMessage: result.ErrorMessage ?? "LLM call failed without a specific error."),
                    Output: null,
                    Abort: MechanicPipelineOutcome.AbortedLlmFailed);
            }

            var validation = _validator.Validate(section, result.Response);
            if (validation.IsValid)
            {
                stopwatch.Stop();
                var completedAt = _timeProvider.GetUtcNow().UtcDateTime;
                var run = new MechanicAnalysisSectionRunEntity
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = request.AnalysisId,
                    Section = (int)section,
                    RunOrder = runOrder,
                    Provider = string.IsNullOrWhiteSpace(result.Cost.Provider) ? request.Provider : result.Cost.Provider,
                    ModelUsed = string.IsNullOrWhiteSpace(result.Cost.ModelId) ? request.Model : result.Cost.ModelId,
                    PromptTokens = result.Usage.PromptTokens,
                    CompletionTokens = result.Usage.CompletionTokens,
                    TotalTokens = result.Usage.TotalTokens,
                    EstimatedCostUsd = decimal.Round(result.Cost.TotalCost, 6, MidpointRounding.AwayFromZero),
                    LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                    Status = 0,
                    ErrorMessage = null,
                    StartedAt = startedAt,
                    CompletedAt = completedAt
                };
                return (run, result.Response, Abort: null);
            }

            lastValidationError = string.Join("; ",
                validation.Violations.Select(v => $"[{v.Rule}] {v.Message}{(v.Path is null ? string.Empty : $" ({v.Path})")}"));

            _logger.LogWarning(
                "Mechanic section '{Section}' attempt {Attempt}/{Total} failed validation: {Error}",
                section, attempt, attempts, lastValidationError);
        }

        stopwatch.Stop();
        var completedAtValidationFail = _timeProvider.GetUtcNow().UtcDateTime;
        var validationFailureRun = new MechanicAnalysisSectionRunEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = request.AnalysisId,
            Section = (int)section,
            RunOrder = runOrder,
            Provider = request.Provider,
            ModelUsed = request.Model,
            PromptTokens = 0,
            CompletionTokens = 0,
            TotalTokens = 0,
            EstimatedCostUsd = 0m,
            LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
            Status = 1,
            ErrorMessage = $"Validation failed after {attempts} attempts: {lastValidationError}",
            StartedAt = startedAt,
            CompletedAt = completedAtValidationFail
        };
        return (validationFailureRun, Output: null, Abort: MechanicPipelineOutcome.AbortedValidation);
    }

    private static MechanicAnalysisSectionRunEntity BuildFailedRun(
        MechanicPipelineRequest request,
        MechanicSection section,
        int runOrder,
        LlmCompletionResult result,
        DateTime startedAt,
        DateTime completedAt,
        long latencyMs,
        int sectionStatus,
        string errorMessage)
    {
        return new MechanicAnalysisSectionRunEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = request.AnalysisId,
            Section = (int)section,
            RunOrder = runOrder,
            Provider = string.IsNullOrWhiteSpace(result.Cost.Provider) ? request.Provider : result.Cost.Provider,
            ModelUsed = string.IsNullOrWhiteSpace(result.Cost.ModelId) ? request.Model : result.Cost.ModelId,
            PromptTokens = result.Usage.PromptTokens,
            CompletionTokens = result.Usage.CompletionTokens,
            TotalTokens = result.Usage.TotalTokens,
            EstimatedCostUsd = decimal.Round(result.Cost.TotalCost, 6, MidpointRounding.AwayFromZero),
            LatencyMs = (int)Math.Min(int.MaxValue, latencyMs),
            Status = sectionStatus,
            ErrorMessage = errorMessage,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
    }

    private static MechanicPipelineResult BuildAbortResult(
        MechanicPipelineOutcome outcome,
        string? detail,
        IReadOnlyList<MechanicAnalysisSectionRunEntity> runs,
        IReadOnlyDictionary<MechanicSection, string> outputs,
        int totalPromptTokens,
        int totalCompletionTokens,
        decimal totalCostUsd) =>
        new(
            Outcome: outcome,
            SectionRuns: runs,
            SectionOutputs: outputs,
            TotalPromptTokens: totalPromptTokens,
            TotalCompletionTokens: totalCompletionTokens,
            TotalCostUsd: decimal.Round(totalCostUsd, 6, MidpointRounding.AwayFromZero),
            AbortDetail: detail);

    private static string BuildUserPrompt(string sectionPrompt, string retrievedContext)
    {
        if (string.IsNullOrWhiteSpace(retrievedContext))
        {
            return sectionPrompt;
        }

        return $"{sectionPrompt}\n\n## Retrieved rulebook chunks\n\n{retrievedContext}\n";
    }
}
