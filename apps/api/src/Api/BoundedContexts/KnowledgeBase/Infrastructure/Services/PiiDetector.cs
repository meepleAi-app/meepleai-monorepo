using System.Diagnostics;
using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Regex-based PII detector for GDPR compliance.
/// Issue #5510: Detects emails, phones, Italian fiscal codes, and credit cards.
/// Performance target: &lt;5ms per scan.
/// </summary>
internal sealed partial class PiiDetector : IPiiDetector
{
    private readonly PiiDetectorOptions _options;
    private readonly ILogger<PiiDetector> _logger;

    // Redaction placeholders per PII type
    private static readonly Dictionary<PiiType, string> s_redactionPlaceholders = new()
    {
        [PiiType.Email] = "[REDACTED_EMAIL]",
        [PiiType.Phone] = "[REDACTED_PHONE]",
        [PiiType.ItalianFiscalCode] = "[REDACTED_FISCAL_CODE]",
        [PiiType.CreditCard] = "[REDACTED_CREDIT_CARD]"
    };

    public PiiDetector(IOptions<PiiDetectorOptions> options, ILogger<PiiDetector> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public PiiScanResult Scan(string text)
    {
        if (string.IsNullOrEmpty(text) || !_options.Enabled)
            return PiiScanResult.Empty(TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        var matches = new List<PiiMatch>();

        if (_options.DetectEmails)
            ScanWithRegex(text, EmailPattern(), PiiType.Email, matches);

        if (_options.DetectFiscalCodes)
            ScanWithRegex(text, ItalianFiscalCodePattern(), PiiType.ItalianFiscalCode, matches);

        if (_options.DetectCreditCards)
            ScanWithRegex(text, CreditCardPattern(), PiiType.CreditCard, matches);

        if (_options.DetectPhones)
            ScanWithRegex(text, PhonePattern(), PiiType.Phone, matches);

        // Remove overlapping matches (later matches that overlap with earlier higher-priority ones)
        RemoveOverlappingMatches(matches);

        sw.Stop();

        if (matches.Count > 0)
        {
            _logger.LogInformation(
                "PII scan found {Count} match(es) in {ElapsedMs:F2}ms: {Types}",
                matches.Count, sw.Elapsed.TotalMilliseconds,
                string.Join(", ", matches.Select(m => m.Type).Distinct()));
        }

        return new PiiScanResult(matches.AsReadOnly(), sw.Elapsed);
    }

    /// <inheritdoc/>
    public string Redact(string text, PiiScanResult result)
    {
        if (string.IsNullOrEmpty(text) || !result.ContainsPii)
            return text;

        // Process matches from end to start to preserve indices
        var sortedMatches = result.Matches
            .OrderByDescending(m => m.StartIndex)
            .ToList();

        var redacted = text;
        foreach (var match in sortedMatches)
        {
            var placeholder = s_redactionPlaceholders[match.Type];
            redacted = string.Concat(
                redacted.AsSpan(0, match.StartIndex),
                placeholder,
                redacted.AsSpan(match.StartIndex + match.Length));
        }

        return redacted;
    }

    /// <inheritdoc/>
    public string ScanAndRedact(string text)
    {
        var result = Scan(text);
        return Redact(text, result);
    }

    /// <summary>
    /// Removes matches that overlap with earlier (higher-priority) matches.
    /// Priority order: Email > FiscalCode > CreditCard > Phone.
    /// </summary>
    private static void RemoveOverlappingMatches(List<PiiMatch> matches)
    {
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var current = matches[i];
            var currentEnd = current.StartIndex + current.Length;

            for (var j = 0; j < i; j++)
            {
                var earlier = matches[j];
                var earlierEnd = earlier.StartIndex + earlier.Length;

                // Check if current overlaps with an earlier (higher-priority) match
                if (current.StartIndex < earlierEnd && currentEnd > earlier.StartIndex)
                {
                    matches.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static void ScanWithRegex(string text, Regex pattern, PiiType type, List<PiiMatch> matches)
    {
        foreach (Match match in pattern.Matches(text))
        {
            matches.Add(new PiiMatch(type, match.Index, match.Length, match.Value));
        }
    }

    // Email: standard email pattern with timeout
    [GeneratedRegex(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPattern();

    // Italian fiscal code (codice fiscale): 16 chars, specific pattern
    // Format: LLLLLLDDLDDLDDDC (L=letter, D=digit, C=check letter)
    [GeneratedRegex(
        @"\b[A-Z]{6}\d{2}[A-EHLMPR-T]\d{2}[A-Z]\d{3}[A-Z]\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItalianFiscalCodePattern();

    // Credit card: 4 groups of 4 digits separated by spaces or dashes, or 13-19 continuous digits
    [GeneratedRegex(
        @"\b\d{4}[\s\-]\d{4}[\s\-]\d{4}[\s\-]\d{1,4}\b|\b\d{13,19}\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CreditCardPattern();

    // Phone numbers: requires leading + (international) or 0 (Italian landline/mobile prefix)
    [GeneratedRegex(
        @"(?:\+\d{1,3}[\s\-]?\(?\d{2,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4})\b|(?:\b0\d{1,3}[\s\-]?\d{3,4}[\s\-]?\d{3,4})\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhonePattern();
}
