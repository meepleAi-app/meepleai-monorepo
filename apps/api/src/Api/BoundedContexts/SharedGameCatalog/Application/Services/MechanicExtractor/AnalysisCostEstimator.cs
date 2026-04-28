using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Default heuristic cost estimator for Mechanic Extractor pipelines (ISSUE-524 / M1.2).
/// </summary>
/// <remarks>
/// Token budgets were calibrated against the v1 prompts:
/// <list type="bullet">
/// <item><description>System prompt (IP policy): ~700 tokens, shared by every section.</description></item>
/// <item><description>Section user prompt (schema + instructions): ~350 tokens/section.</description></item>
/// <item><description>Retrieved context: supplied by caller via <see cref="AnalysisCostEstimateInput.TotalRetrievedPromptTokens"/>.</description></item>
/// <item><description>Output budget: aligned to <c>MechanicAnalysisPipeline.SectionMaxTokens = 4000</c> so the
/// pre-flight projection matches the runtime ceiling and admins do not see surprise mid-run aborts
/// when sections legitimately use the full token budget on dense rulebooks (e.g. Dune: Imperium).</description></item>
/// </list>
/// The estimator purposely over-projects so that cost caps bite early rather than mid-run.
/// </remarks>
internal sealed class AnalysisCostEstimator : IAnalysisCostEstimator
{
    private const int SystemPromptTokens = 700;
    private const int SectionInstructionTokens = 350;
    // Aligned to MechanicAnalysisPipeline.SectionMaxTokens so estimator does not under-quote the runtime cap.
    private const int DefaultSectionOutputTokens = 4000;
    private const int FaqOutputTokens = 4000;

    public AnalysisCostEstimate Estimate(AnalysisCostEstimateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Sections.Count == 0)
        {
            throw new ArgumentException("At least one section must be targeted.", nameof(input));
        }

        var chunksPerSection = Math.Max(0, input.TotalRetrievedPromptTokens) / input.Sections.Count;

        var perSection = new Dictionary<MechanicSection, SectionCostProjection>(input.Sections.Count);
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;
        decimal totalCost = 0m;

        foreach (var section in input.Sections)
        {
            var promptTokens = SystemPromptTokens + SectionInstructionTokens + chunksPerSection;
            var completionTokens = section == MechanicSection.Faq ? FaqOutputTokens : DefaultSectionOutputTokens;

            var inputCost = promptTokens / 1_000_000m * input.InputCostPerMillionTokens;
            var outputCost = completionTokens / 1_000_000m * input.OutputCostPerMillionTokens;
            var sectionCost = decimal.Round(inputCost + outputCost, 6, MidpointRounding.AwayFromZero);

            perSection[section] = new SectionCostProjection(promptTokens, completionTokens, sectionCost);
            totalPromptTokens += promptTokens;
            totalCompletionTokens += completionTokens;
            totalCost += sectionCost;
        }

        return new AnalysisCostEstimate(
            ProjectedPromptTokens: totalPromptTokens,
            ProjectedCompletionTokens: totalCompletionTokens,
            ProjectedTotalTokens: totalPromptTokens + totalCompletionTokens,
            ProjectedCostUsd: decimal.Round(totalCost, 6, MidpointRounding.AwayFromZero),
            PerSection: perSection);
    }
}
