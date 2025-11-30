using Api.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Api.Services.Rag;

/// <summary>
/// Validates citation references in RAG responses
/// </summary>
public class CitationExtractorService : ICitationExtractorService
{
    private readonly ILogger<CitationExtractorService> _logger;

    public CitationExtractorService(ILogger<CitationExtractorService> logger)
    {
        _logger = logger;
    }

    public bool ValidateCitations(List<Snippet> snippets, string answer)
    {
        // Extract [1], [2], etc. from answer
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        var citationPattern = @"\[(\d+)\]";
        var matches = Regex.Matches(answer, citationPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        if (matches.Count == 0)
        {
            // No citations found - this is acceptable for some queries
            return true;
        }

        var allValid = true;

        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int index))
            {
                // Citations are 1-indexed
                if (index < 1 || index > snippets.Count)
                {
                    _logger.LogWarning("Invalid citation index: {Index} (max: {MaxIndex})", index, snippets.Count);
                    allValid = false;
                }
            }
        }

        if (allValid)
        {
            _logger.LogDebug("Validated {Count} citations in answer", matches.Count);
        }

        return allValid;
    }
}
