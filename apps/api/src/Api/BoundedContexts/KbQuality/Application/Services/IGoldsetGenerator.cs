using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Generates a goldset (synthetic Q&amp;A pairs) for a single PDF doc during Phase A
/// of per-doc evaluation (#1675). Deterministic intent is documented via a
/// <c>seed</c> embedded in the prompt body per plan amendment A5 (no <c>seed</c>
/// parameter on <c>ILlmClient</c>; rely on <c>temperature=0</c> + prompt-resident
/// seed; accept ±0.05 metric variance across re-runs).
/// </summary>
public interface IGoldsetGenerator
{
    /// <summary>
    /// Produce a goldset for <paramref name="doc"/> using <paramref name="seed"/>
    /// as a documented determinism marker baked into the LLM prompt body.
    /// </summary>
    /// <param name="doc">Read-model snapshot of the PDF doc to evaluate.</param>
    /// <param name="seed">Determinism marker embedded in the prompt body (A5).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GoldsetGenerationResult> GenerateAsync(
        PdfDocSnapshot doc,
        long seed,
        CancellationToken ct);
}
