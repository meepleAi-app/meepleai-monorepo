using Api.BoundedContexts.GameManagement.Application.DTOs;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Domain service for parsing atomic rules from various formats.
/// Handles JSON parsing and plain text extraction.
/// </summary>
public partial class RuleAtomParsingDomainService
{
    /// <summary>
    /// Parses atomic rules from JSON format.
    /// Expected format: array of strings representing individual rules.
    /// </summary>
    public IReadOnlyList<string> ParseAtomicRulesFromJson(string? atomicRulesJson)
    {
        if (string.IsNullOrWhiteSpace(atomicRulesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<string>>(atomicRulesJson);
            if (rules == null)
            {
                return Array.Empty<string>();
            }

            return rules
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Parses rules from extracted text by splitting on newlines.
    /// Filters out empty lines.
    /// </summary>
    public IReadOnlyList<string> ParseRulesFromExtractedText(string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return Array.Empty<string>();
        }

        return extractedText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    /// <summary>
    /// Creates a RuleAtomDto from raw text with optional page extraction.
    /// Detects page numbers from patterns like "[Table on page 5]" or "page 3".
    /// </summary>
    public RuleAtomDto CreateRuleAtom(string rawText, int index)
    {
        string? page = null;
        string cleanedText = rawText.Trim();

        // Pattern: [Table on page 5] ...
        var tableMatch = TablePageRegex().Match(cleanedText);
        if (tableMatch.Success)
        {
            page = tableMatch.Groups["page"].Value;
            cleanedText = tableMatch.Groups["rest"].Value.Trim();
        }
        else
        {
            // Pattern: ... page 3 ...
            var pageMatch = PageNumberRegex().Match(cleanedText);
            if (pageMatch.Success)
            {
                page = pageMatch.Groups["page"].Value;
            }
        }

        return new RuleAtomDto(
            Id: $"r{index}",
            Text: cleanedText,
            Section: null,
            Page: page,
            Line: null
        );
    }

    // FIX MA0009: Add timeout to prevent ReDoS attacks
    [GeneratedRegex(@"^\[Table on page (?<page>\d+)\]\s*(?<rest>.+)$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TablePageRegex();

    [GeneratedRegex(@"page\s+(?<page>\d+)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageNumberRegex();
}
