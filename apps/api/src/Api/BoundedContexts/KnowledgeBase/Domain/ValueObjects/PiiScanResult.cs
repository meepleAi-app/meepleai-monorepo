namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Result of a PII scan on text content.
/// Issue #5510: GDPR compliance — PII detection before OpenRouter calls.
/// </summary>
public sealed class PiiScanResult
{
    public IReadOnlyList<PiiMatch> Matches { get; }
    public bool ContainsPii => Matches.Count > 0;
    public TimeSpan ScanDuration { get; }

    public PiiScanResult(IReadOnlyList<PiiMatch> matches, TimeSpan scanDuration)
    {
        Matches = matches ?? throw new ArgumentNullException(nameof(matches));
        ScanDuration = scanDuration;
    }

    public static PiiScanResult Empty(TimeSpan duration) => new(Array.Empty<PiiMatch>(), duration);
}

/// <summary>
/// A single PII match found in scanned text.
/// </summary>
public sealed class PiiMatch
{
    public PiiType Type { get; }
    public int StartIndex { get; }
    public int Length { get; }
    public string OriginalValue { get; }

    public PiiMatch(PiiType type, int startIndex, int length, string originalValue)
    {
        Type = type;
        StartIndex = startIndex;
        Length = length;
        OriginalValue = originalValue ?? throw new ArgumentNullException(nameof(originalValue));
    }
}

/// <summary>
/// Types of PII that can be detected.
/// </summary>
public enum PiiType
{
    Email,
    Phone,
    ItalianFiscalCode,
    CreditCard
}
