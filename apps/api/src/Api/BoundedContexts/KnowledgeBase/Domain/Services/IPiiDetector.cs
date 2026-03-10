using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Detects and redacts PII from text before sending to external LLM providers.
/// Issue #5510: GDPR compliance — PII detection and stripping before OpenRouter calls.
/// </summary>
public interface IPiiDetector
{
    /// <summary>
    /// Scans text for PII matches.
    /// </summary>
    PiiScanResult Scan(string text);

    /// <summary>
    /// Redacts detected PII from text, replacing with typed placeholders.
    /// </summary>
    string Redact(string text, PiiScanResult result);

    /// <summary>
    /// Convenience method: scans and redacts in one call.
    /// </summary>
    string ScanAndRedact(string text);
}
