using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Unit tests for <see cref="MechanicAnalysisPipeline.RunAsync"/> run-all-retain behaviour
/// introduced by #2782 FU-1 (D3). Asserts:
///   (a) An ordinary guardrail failure on a well-formed section is RETAINED (Status=3, output kept,
///       final-attempt RuleOutcomes captured) and does NOT hard-abort the pipeline (Outcome=Succeeded).
///   (b) A never-well-formed section (malformed JSON on every attempt) is left ABSENT from
///       SectionOutputs + SectionOutcomes and does not abort the run.
///   (c) A grounding-UNAVAILABLE (embedding outage) final failure hard-aborts (fail-closed IP
///       protection) even though ordinary guardrail fails are now advisory.
///   (d) An LLM hard-failure still aborts with AbortedLlmFailed (unchanged).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisPipelineTests
{
    // Well-formed JSON envelope — parses cleanly so the section reaches the guardrail chain.
    private const string WellFormedJson = """
        { "summary": { "text": "x", "citations": [] } }
        """;

    private const string MalformedJson = "{ not json ::: }";

    [Fact]
    public async Task RunAsync_GuardrailFailSection_IsRetained_NotAborted_WithStatus3()
    {
        // (a) LLM returns well-formed JSON every attempt; validator ALWAYS fails on T2 (ordinary
        // guardrail) carrying RuleOutcomes [T1 pass, T2 fail, T3a/T3b/T4 notRun].
        var ruleOutcomes = new MechanicRuleOutcome[]
        {
            new("T1", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T2", "fail", "long verbatim", "$.summary.text", null,
                new[] { new MechanicValidationViolation("T2", "long verbatim", "$.summary.text") }),
            new("T3a", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T3b", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T4", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>())
        };
        var validation = MechanicValidationResult.Invalid(
            new[] { new MechanicValidationViolation("T2", "long verbatim", "$.summary.text") },
            ruleOutcomes);

        var pipeline = BuildPipeline(
            llmResponse: WellFormedJson,
            validation: validation);

        var result = await pipeline.RunAsync(
            BuildRequest(MechanicSection.Summary), CancellationToken.None);

        result.Outcome.Should().Be(MechanicPipelineOutcome.Succeeded);
        result.SectionOutputs.Should().ContainKey(MechanicSection.Summary);
        result.SectionOutcomes.Should().ContainKey(MechanicSection.Summary);
        result.SectionOutcomes[MechanicSection.Summary]
            .Single(o => o.Rule == "T2").Outcome.Should().Be("fail");
        result.SectionRuns
            .Single(r => r.Section == (int)MechanicSection.Summary).Status.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_RetainedGuardrailSection_ProducesStatus3RunWithFlagSummary()
    {
        // #2782 D9: a retained (Status=3) section run must carry an honest ErrorMessage
        // summarizing which rule families failed (telemetry legibility) — even though the
        // Status=3 DB CHECK constraint itself does not require a non-null ErrorMessage.
        var ruleOutcomes = new MechanicRuleOutcome[]
        {
            new("T1", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T2", "fail", "long verbatim", "$.summary.text", null,
                new[] { new MechanicValidationViolation("T2", "long verbatim", "$.summary.text") }),
            new("T3a", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T3b", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>()),
            new("T4", "notRun", null, null, null, Array.Empty<MechanicValidationViolation>())
        };
        var validation = MechanicValidationResult.Invalid(
            new[] { new MechanicValidationViolation("T2", "long verbatim", "$.summary.text") },
            ruleOutcomes);

        var pipeline = BuildPipeline(
            llmResponse: WellFormedJson,
            validation: validation);

        var result = await pipeline.RunAsync(
            BuildRequest(MechanicSection.Summary), CancellationToken.None);

        var run = result.SectionRuns.Single(r => r.Section == (int)MechanicSection.Summary);
        run.Status.Should().Be(3);
        run.ErrorMessage.Should().Contain("T2");
        // Honest telemetry: the message must state the section was RETAINED (not merely
        // "failed") and must summarize the DISTINCT failed rule families from RuleOutcomes,
        // not a raw dump of the last attempt's Violations collection.
        run.ErrorMessage.Should().Be("Retained with guardrail flags: T2");
    }

    [Fact]
    public async Task RunAsync_WellFormedFail_LeavesSectionAbsent_NoAbort()
    {
        // (b) LLM always returns non-JSON → validator (real path) yields Invalid([well_formed])
        // with empty RuleOutcomes. Section must be ABSENT + pipeline continues (no hard abort).
        var wellFormedOnly = MechanicValidationResult.Invalid(
            new[] { new MechanicValidationViolation("well_formed", "Output is not valid JSON") });

        var pipeline = BuildPipeline(
            llmResponse: MalformedJson,
            validation: wellFormedOnly);

        var result = await pipeline.RunAsync(
            BuildRequest(MechanicSection.Summary), CancellationToken.None);

        result.Outcome.Should().Be(MechanicPipelineOutcome.Succeeded);
        result.SectionOutputs.Should().NotContainKey(MechanicSection.Summary);
        result.SectionOutcomes.Should().NotContainKey(MechanicSection.Summary);
    }

    [Fact]
    public async Task RunAsync_GroundingUnavailable_HardAborts()
    {
        // (c) Final failure carries T3_grounding_unavailable (embedding OUTAGE) → hard abort even
        // though the JSON is well-formed and this is otherwise an "advisory" mode.
        var groundingUnavailable = MechanicValidationResult.Invalid(
            new[] { new MechanicValidationViolation("T3_grounding_unavailable", "Embedding service unavailable") },
            Array.Empty<MechanicRuleOutcome>());

        var pipeline = BuildPipeline(
            llmResponse: WellFormedJson,
            validation: groundingUnavailable);

        var result = await pipeline.RunAsync(
            BuildRequest(MechanicSection.Summary), CancellationToken.None);

        result.Outcome.Should().Be(MechanicPipelineOutcome.AbortedValidation);
        result.SectionOutputs.Should().NotContainKey(MechanicSection.Summary);
    }

    [Fact]
    public async Task RunAsync_LlmFailure_AbortsWithLlmFailed()
    {
        // (d) LLM hard-fails (Success=false) → AbortedLlmFailed, unchanged.
        var pipeline = BuildPipeline(
            llmResult: LlmCompletionResult.CreateFailure("provider exploded"),
            validation: MechanicValidationResult.Valid());

        var result = await pipeline.RunAsync(
            BuildRequest(MechanicSection.Summary), CancellationToken.None);

        result.Outcome.Should().Be(MechanicPipelineOutcome.AbortedLlmFailed);
        result.SectionOutputs.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_CostCap_Aborts()
    {
        // (e) Cumulative cost after the section exceeds the cap → AbortedCostCap (unchanged).
        // The section validates cleanly (no guardrail interference), but the per-attempt cost
        // blows the effective cap.
        var pipeline = BuildPipeline(
            llmResult: LlmCompletionResult.CreateSuccess(
                WellFormedJson,
                cost: new LlmCost
                {
                    InputCost = 5m,
                    OutputCost = 5m,
                    ModelId = "test-model",
                    Provider = "test-provider"
                }),
            validation: MechanicValidationResult.Valid());

        var request = BuildRequest(MechanicSection.Summary) with { EffectiveCostCapUsd = 1m };

        var result = await pipeline.RunAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(MechanicPipelineOutcome.AbortedCostCap);
    }

    [Fact]
    public async Task RunAsync_UsesMultiProviderFallbackPath_NotSingleShot()
    {
        // #2961: the ME pipeline must call the resilient multi-provider fallback completion
        // (GenerateCompletionWithModelFallbackAsync), so a single provider outage (e.g. DeepSeek
        // 402) can fall back instead of hard-aborting the analysis. Both LLM methods are mocked so
        // the pipeline completes whichever it calls — the assertion is on WHICH one it called.
        var llm = new Mock<ILlmService>();
        var okResult = LlmCompletionResult.CreateSuccess(
            WellFormedJson,
            usage: new LlmUsage(10, 5, 15),
            cost: new LlmCost { InputCost = 0.0001m, OutputCost = 0.0001m, ModelId = "test-model", Provider = "test-provider" });
        llm
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(okResult);
        llm
            .Setup(s => s.GenerateCompletionWithModelFallbackAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(okResult);

        var pipeline = BuildPipelineFrom(llm, MechanicValidationResult.Valid());

        await pipeline.RunAsync(BuildRequest(MechanicSection.Summary), CancellationToken.None);

        llm.Verify(
            s => s.GenerateCompletionWithModelFallbackAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- fakes / builders -------------------------------------------------

    private static MechanicPipelineRequest BuildRequest(params MechanicSection[] sections) =>
        new(
            AnalysisId: Guid.NewGuid(),
            SharedGameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            PromptVersion: "v1.0.0",
            Sections: sections,
            RetrievedContextBySection: new Dictionary<MechanicSection, string>(),
            Provider: "test-provider",
            Model: "test-model",
            EffectiveCostCapUsd: 100m,
            InputCostPerMillionTokens: 0.14m,
            OutputCostPerMillionTokens: 0.28m);

    private static MechanicAnalysisPipeline BuildPipeline(
        MechanicValidationResult validation,
        string? llmResponse = null,
        LlmCompletionResult? llmResult = null)
    {
        var llm = new Mock<ILlmService>();
        var effectiveLlmResult = llmResult
            ?? LlmCompletionResult.CreateSuccess(
                llmResponse ?? WellFormedJson,
                usage: new LlmUsage(10, 5, 15),
                cost: new LlmCost
                {
                    InputCost = 0.0001m,
                    OutputCost = 0.0001m,
                    ModelId = "test-model",
                    Provider = "test-provider"
                });

        // #2961: the pipeline now calls the fallback-enabled path; mock BOTH so behaviour tests
        // are agnostic to which completion method the pipeline invokes.
        llm
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectiveLlmResult);
        llm
            .Setup(s => s.GenerateCompletionWithModelFallbackAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectiveLlmResult);

        return BuildPipelineFrom(llm, validation);
    }

    private static MechanicAnalysisPipeline BuildPipelineFrom(
        Mock<ILlmService> llm,
        MechanicValidationResult validation)
    {
        var promptProvider = new Mock<IMechanicPromptProvider>();
        promptProvider.SetupGet(p => p.PromptVersion).Returns("v1.0.0");
        promptProvider.Setup(p => p.GetSystemPrompt()).Returns("system");
        promptProvider.Setup(p => p.GetSectionPrompt(It.IsAny<MechanicSection>())).Returns("section prompt");

        var validator = new Mock<IMechanicOutputValidator>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<MechanicGuardrailContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validation);

        return new MechanicAnalysisPipeline(
            llm.Object,
            promptProvider.Object,
            validator.Object,
            TimeProvider.System,
            Options.Create(new MechanicGuardrailOptions()),
            NullLogger<MechanicAnalysisPipeline>.Instance);
    }
}
