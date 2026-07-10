using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    // Per-section max_tokens cap. ADR-051 originally targeted 1500, but live runs on
    // dense rulebooks (e.g. Dune: Imperium Setup section) truncate at the 1500 boundary
    // — the validator catches it via the well_formed rule ("Expected depth to be zero")
    // and the section ends up PartiallyExtracted. Bumping to 4000 preserves headroom for
    // long Setup/MechanicDetails JSON without materially affecting cost (DeepSeek pricing
    // scales linearly and most sections still complete well under the cap).
    private const int SectionMaxTokens = 4000;
    // NOTE: Temperature (target 0.2) is still threaded through ILlmService defaults.
    // We will switch to explicit JSON schema strict mode via ILlmClient in M1.3.

    private readonly ILlmService _llmService;
    private readonly IMechanicPromptProvider _promptProvider;
    private readonly IMechanicOutputValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MechanicAnalysisPipeline> _logger;
    private readonly MechanicGuardrailOptions _options;

    public MechanicAnalysisPipeline(
        ILlmService llmService,
        IMechanicPromptProvider promptProvider,
        IMechanicOutputValidator validator,
        TimeProvider timeProvider,
        IOptions<MechanicGuardrailOptions> options,
        ILogger<MechanicAnalysisPipeline> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicPipelineResult> RunAsync(MechanicPipelineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sectionRuns = new List<MechanicAnalysisSectionRunEntity>(request.Sections.Count);
        var outputs = new Dictionary<MechanicSection, string>(request.Sections.Count);
        var sectionOutcomesMap = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>();
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

            var (sectionRun, sectionOutput, sectionAbort, sectionOutcomes) = await RunSectionAsync(
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

            if (sectionOutcomes.Count > 0)
            {
                sectionOutcomesMap[section] = sectionOutcomes;
            }

            if (sectionAbort is not null)
            {
                return BuildAbortResult(sectionAbort.Value, sectionRun.ErrorMessage, sectionRuns, outputs,
                    sectionOutcomesMap, totalPromptTokens, totalCompletionTokens, totalCostUsd);
            }

            if (totalCostUsd > request.EffectiveCostCapUsd)
            {
                _logger.LogWarning(
                    "Mechanic pipeline {AnalysisId} aborted at section {Section}: cumulative cost {Cost:F4} > cap {Cap:F4}.",
                    request.AnalysisId, section, totalCostUsd, request.EffectiveCostCapUsd);

                return BuildAbortResult(
                    MechanicPipelineOutcome.AbortedCostCap,
                    $"Cumulative cost {totalCostUsd:F6} USD exceeded effective cap {request.EffectiveCostCapUsd:F6} USD after section '{section}'.",
                    sectionRuns, outputs, sectionOutcomesMap, totalPromptTokens, totalCompletionTokens, totalCostUsd);
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
            AbortDetail: null)
        {
            SectionOutcomes = sectionOutcomesMap
        };
    }

    private async Task<(MechanicAnalysisSectionRunEntity Run, string? Output,
        MechanicPipelineOutcome? Abort, IReadOnlyList<MechanicRuleOutcome> Outcomes)>
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
        string? lastOutputHash = null;
        var currentSystemPrompt = systemPrompt;
        var attempts = _options.MaxRetriesPerSection + 1;

        // #2782 D3: retain the last well-formed output + its FINAL-attempt validation so the
        // post-loop tail can classify (never-well-formed vs guardrail-fail vs grounding-outage)
        // WITHOUT re-parsing or re-validating.
        MechanicValidationResult? lastValidation = null;
        string? lastCleanedResponse = null;

        // Accumulate tokens/cost across all attempts (retries share the T8 cap, AC-6).
        var accPromptTokens = 0;
        var accCompletionTokens = 0;
        decimal accCostUsd = 0m;
        LlmCompletionResult? lastResult = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _llmService.GenerateCompletionWithModelAsync(
                explicitModel: request.Model,
                systemPrompt: currentSystemPrompt,
                userPrompt: userPrompt,
                source: RequestSource.Manual,
                maxTokens: SectionMaxTokens,
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
                    Abort: MechanicPipelineOutcome.AbortedLlmFailed,
                    Outcomes: Array.Empty<MechanicRuleOutcome>());
            }

            lastResult = result;
            accPromptTokens += result.Usage.PromptTokens;
            accCompletionTokens += result.Usage.CompletionTokens;
            accCostUsd += result.Cost.TotalCost;

            // DeepSeek (and some other chat models) wrap JSON in markdown code fences
            // (```json ... ``` or ``` ... ```). The validator and downstream parser expect
            // raw JSON, so strip fences before validation and persist the cleaned output.
            var cleanedResponse = StripCodeFences(result.Response);
            var validation = await ValidateSectionAsync(
                request, section, cleanedResponse, attempt - 1, cancellationToken).ConfigureAwait(false);

            // Capture the final state on every attempt for post-loop classification (#2782 D3).
            lastValidation = validation;
            lastCleanedResponse = cleanedResponse;

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
                    PromptTokens = accPromptTokens,
                    CompletionTokens = accCompletionTokens,
                    TotalTokens = accPromptTokens + accCompletionTokens,
                    EstimatedCostUsd = decimal.Round(accCostUsd, 6, MidpointRounding.AwayFromZero),
                    LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                    Status = 0,
                    ErrorMessage = null,
                    StartedAt = startedAt,
                    CompletedAt = completedAt
                };
                return (run, cleanedResponse, Abort: null, Outcomes: validation.RuleOutcomes);
            }

            lastValidationError = string.Join("; ",
                validation.Violations.Select(v => $"[{v.Rule}] {v.Message}{(v.Path is null ? string.Empty : $" ({v.Path})")}"));

            _logger.LogWarning(
                "Mechanic section '{Section}' attempt {Attempt}/{Total} failed validation: {Error}",
                section, attempt, attempts, lastValidationError);

            // AC-6 stable-output detection: identical regeneration → stop early.
            var outputHash = ComputeHash(cleanedResponse);
            if (string.Equals(outputHash, lastOutputHash, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Mechanic section '{Section}' produced identical output on retry; breaking loop (RegenerationDivergent=false).",
                    section);
                break;
            }
            lastOutputHash = outputHash;

            // Augment the system prompt with the violations (JSON) for self-correction.
            currentSystemPrompt = AugmentSystemPrompt(systemPrompt, validation.Violations);
        }

        stopwatch.Stop();
        var completedAtValidationFail = _timeProvider.GetUtcNow().UtcDateTime;

        // #2782 D3: classify the final validation failure WITHOUT re-parsing/re-validating.
        // - Never well-formed (malformed JSON / empty every attempt): section stays ABSENT.
        // - Grounding outage (embedding down): HARD ABORT (fail-closed IP protection).
        // - Ordinary guardrail fail: RETAIN the last well-formed output + its RuleOutcomes (Status=3).
        var neverWellFormed = lastValidation is null
            || lastValidation.Violations.All(v => string.Equals(v.Rule, "well_formed", StringComparison.Ordinal));
        var groundingUnavailable = lastValidation is not null
            && lastValidation.Violations.Any(v => string.Equals(v.Rule, "T3_grounding_unavailable", StringComparison.Ordinal));

        // Status: 3 (RetainedWithGuardrailFlags) only when we actually retain; else 1 (Failed).
        var failureStatus = !neverWellFormed && !groundingUnavailable ? 3 : 1;

        var validationFailureRun = new MechanicAnalysisSectionRunEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = request.AnalysisId,
            Section = (int)section,
            RunOrder = runOrder,
            Provider = lastResult is null || string.IsNullOrWhiteSpace(lastResult.Cost.Provider) ? request.Provider : lastResult.Cost.Provider,
            ModelUsed = lastResult is null || string.IsNullOrWhiteSpace(lastResult.Cost.ModelId) ? request.Model : lastResult.Cost.ModelId,
            PromptTokens = accPromptTokens,
            CompletionTokens = accCompletionTokens,
            TotalTokens = accPromptTokens + accCompletionTokens,
            EstimatedCostUsd = decimal.Round(accCostUsd, 6, MidpointRounding.AwayFromZero),
            LatencyMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
            Status = failureStatus,
            ErrorMessage = $"Validation failed after {attempts} attempts: {lastValidationError}",
            StartedAt = startedAt,
            CompletedAt = completedAtValidationFail
        };

        if (groundingUnavailable)
        {
            // Fail-closed: an embedding OUTAGE cannot certify grounding, so we hard-abort even
            // under advisory mode. Salvaged claims must not leak IP that was never grounded.
            return (validationFailureRun, Output: null, Abort: MechanicPipelineOutcome.AbortedValidation,
                Outcomes: Array.Empty<MechanicRuleOutcome>());
        }

        if (neverWellFormed)
        {
            // Section never produced parseable output — leave it absent (no outcomes, no output).
            return (validationFailureRun, Output: null, Abort: null,
                Outcomes: Array.Empty<MechanicRuleOutcome>());
        }

        // Ordinary guardrail failure on a well-formed section → retain (advisory). No abort.
        return (validationFailureRun, Output: lastCleanedResponse, Abort: null,
            Outcomes: lastValidation!.RuleOutcomes);
    }

    /// <summary>
    /// Parses the cleaned LLM output, builds the guardrail context (source pool + page count +
    /// options), and runs the validator chain. A JSON parse failure becomes a
    /// <c>well_formed</c> violation (so the retry loop re-prompts).
    /// </summary>
    private async Task<MechanicValidationResult> ValidateSectionAsync(
        MechanicPipelineRequest request,
        MechanicSection section,
        string cleanedResponse,
        int retryCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cleanedResponse))
        {
            return MechanicValidationResult.Invalid(new[]
            {
                new MechanicValidationViolation("well_formed", "Output is empty or whitespace.")
            });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleanedResponse);
        }
        catch (JsonException ex)
        {
            return MechanicValidationResult.Invalid(new[]
            {
                new MechanicValidationViolation("well_formed", $"Output is not valid JSON: {ex.Message}")
            });
        }

        try
        {
            var chunks = request.SourceChunksBySection.TryGetValue(section, out var sc)
                ? sc
                : Array.Empty<MechanicSourceChunk>();
            var context = new MechanicGuardrailContext(
                section, doc.RootElement, chunks, request.PdfPageCount, _options)
            {
                AnalysisId = request.AnalysisId,
                RetryCount = retryCount
            };
            return await _validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            doc.Dispose();
        }
    }

    private static string AugmentSystemPrompt(
        string baseSystemPrompt, IReadOnlyList<MechanicValidationViolation> violations)
    {
        var payload = JsonSerializer.Serialize(
            violations.Select(v => new { rule = v.Rule, message = v.Message, path = v.Path }));
        return baseSystemPrompt
            + "\n\n## PREVIOUS_ATTEMPT_VIOLATIONS\n"
            + "Your previous output was rejected by the guardrails below. Fix every violation and "
            + "return corrected JSON only (no prose):\n"
            + payload;
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes);
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
        IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>> sectionOutcomes,
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
            AbortDetail: detail)
        {
            SectionOutcomes = sectionOutcomes
        };

    private static string BuildUserPrompt(string sectionPrompt, string retrievedContext)
    {
        if (string.IsNullOrWhiteSpace(retrievedContext))
        {
            return sectionPrompt;
        }

        return $"{sectionPrompt}\n\n## Retrieved rulebook chunks\n\n{retrievedContext}\n";
    }

    /// <summary>
    /// Strips markdown code fences (```json ... ``` or ``` ... ```) commonly added by
    /// chat-style LLMs (DeepSeek, GPT-4 chat, Claude, etc.) when asked for JSON output.
    /// Returns the inner content trimmed of whitespace. If no fences are present, returns
    /// the input trimmed.
    /// </summary>
    private static string StripCodeFences(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw ?? string.Empty;
        }

        var trimmed = raw.Trim();

        // Must start with a triple backtick to be a fenced block
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        // Skip the opening fence + optional language tag (e.g. ```json\n)
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            // Single-line fenced output is malformed; return as-is trimmed
            return trimmed;
        }

        var afterOpeningFence = trimmed.Substring(firstNewline + 1);

        // Find the closing fence
        var closingFenceIndex = afterOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFenceIndex < 0)
        {
            // No closing fence; return what we have after the opener
            return afterOpeningFence.Trim();
        }

        return afterOpeningFence.Substring(0, closingFenceIndex).Trim();
    }
}
