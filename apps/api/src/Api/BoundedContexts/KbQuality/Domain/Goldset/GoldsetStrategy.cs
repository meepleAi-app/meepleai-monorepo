namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

/// <summary>
/// Goldset generation strategy (#1675 §3.3, D-C/D-F).
/// Fase 1 ships only LlmAutoGen; Manual + Feedback added when D-F trigger fires.
/// </summary>
public enum GoldsetStrategy
{
    LlmAutoGen,
    Manual,
    Feedback,
}
