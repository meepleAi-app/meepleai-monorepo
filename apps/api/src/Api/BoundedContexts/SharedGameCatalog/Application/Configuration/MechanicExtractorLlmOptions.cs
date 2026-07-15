namespace Api.BoundedContexts.SharedGameCatalog.Application.Configuration;

/// <summary>
/// Default LLM provider/model for the Mechanic Extractor generation pipeline (ADR-051 / #2951).
/// Bound from configuration section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
/// Making the default config-driven (instead of a hardcoded <c>const</c>) lets an operator switch
/// the provider without a redeploy when the wired default is unavailable — e.g. DeepSeek credit
/// exhausted returns 402 on <c>chat/completions</c>, which the ME path surfaces as a hard abort
/// (<c>Rejected</c>) with no automatic multi-provider fallback. Routing is by model name
/// (<c>ILlmClient.SupportsModel</c>), so the model alone selects the provider. The defaults below
/// preserve the pre-#2951 behaviour when the section is absent.
/// </remarks>
public sealed class MechanicExtractorLlmOptions
{
    public const string SectionName = "MechanicExtractorLlm";

    /// <summary>Provider label recorded on the analysis (ADR-007 wired default: DeepSeek).</summary>
    public string DefaultProvider { get; init; } = "DeepSeek";

    /// <summary>Model id used for generation; selects the provider via <c>SupportsModel</c>.</summary>
    public string DefaultModel { get; init; } = "deepseek-chat";
}
