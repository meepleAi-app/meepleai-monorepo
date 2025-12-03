using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// ADR-016 Phase 1: Domain service that selects appropriate chunking strategy
/// based on content characteristics.
/// </summary>
public sealed class ChunkingStrategySelector
{
    // Thresholds for content classification
    private const double TableDensityThreshold = 0.08; // 8% of content is table-like (pipes/tabs)
    private const double ListDensityThreshold = 0.20;  // 20% of lines have list patterns
    private const int ShortContentThreshold = 500;     // Characters
    private const int MinTablePatternCount = 6;        // Minimum patterns to trigger dense

    // Pre-compiled regex patterns for list detection (thread-safe, with timeout for ReDoS protection)
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex BulletPointRegex = new(@"^\s*[-*•]\s+", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex NumberedListRegex = new(@"^\s*\d+[.):]\s+", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex LetterListRegex = new(@"^\s*[a-zA-Z][.):]\s+", RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex CheckboxRegex = new(@"^\s*\[.?\]\s+", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    /// Selects the optimal chunking configuration based on content analysis.
    /// </summary>
    /// <param name="content">The text content to analyze.</param>
    /// <param name="elementTypes">Optional element types from extraction (table, list, text, heading).</param>
    /// <returns>The recommended chunking configuration.</returns>
    public ChunkingConfiguration SelectStrategy(string content, IEnumerable<string>? elementTypes = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ChunkingConfiguration.Baseline;

        var types = elementTypes?.ToList() ?? new List<string>();

        // Priority 1: If explicit element types indicate dense content
        if (HasHighDenseElementRatio(types))
        {
            return ChunkingConfiguration.Dense;
        }

        // Priority 2: Analyze content patterns
        var metrics = AnalyzeContent(content);

        // Dense config for tables and complex lists
        // Either high density OR sufficient absolute count of table patterns
        var hasTablePatterns = metrics.TableDensity >= TableDensityThreshold ||
                              metrics.TablePatternCount >= MinTablePatternCount;
        if (hasTablePatterns || metrics.ListDensity >= ListDensityThreshold)
        {
            return ChunkingConfiguration.Dense;
        }

        // Sparse config for long narrative content with few breaks
        if (metrics.IsNarrative && content.Length > ShortContentThreshold)
        {
            return ChunkingConfiguration.Sparse;
        }

        // Default: Baseline for general content
        return ChunkingConfiguration.Baseline;
    }

    /// <summary>
    /// Checks if element types indicate high-density content (tables, lists).
    /// </summary>
    private static bool HasHighDenseElementRatio(IReadOnlyList<string> elementTypes)
    {
        if (elementTypes.Count == 0)
            return false;

        var denseTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "table", "list", "list_item", "table_cell", "table_row"
        };

        var denseCount = elementTypes.Count(t => denseTypes.Contains(t));
        var ratio = (double)denseCount / elementTypes.Count;

        return ratio >= 0.30; // 30% or more dense elements
    }

    /// <summary>
    /// Analyzes content for structural patterns.
    /// </summary>
    private static ContentMetrics AnalyzeContent(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var totalChars = content.Length;

        // Table detection: pipe characters, tab-separated values, column patterns
        var tablePatternCount = CountTablePatterns(content);
        var tableDensity = totalChars > 0 ? (double)tablePatternCount / totalChars : 0;

        // List detection: bullet points, numbered lists
        var listPatternCount = CountListPatterns(lines);
        var listDensity = lines.Length > 0 ? (double)listPatternCount / lines.Length : 0;

        // Narrative detection: long paragraphs with few structural elements
        var avgLineLength = lines.Length > 0 ? lines.Average(l => l.Length) : 0;
        var paragraphBreaks = CountParagraphBreaks(content);
        var isNarrative = avgLineLength > 80 && tableDensity < 0.05 && listDensity < 0.10;

        return new ContentMetrics
        {
            TableDensity = tableDensity,
            TablePatternCount = tablePatternCount,
            ListDensity = listDensity,
            IsNarrative = isNarrative,
            ParagraphCount = paragraphBreaks + 1
        };
    }

    private static int CountTablePatterns(string content)
    {
        var count = 0;

        // Count pipe characters (Markdown tables)
        count += content.Count(c => c == '|');

        // Count tab characters (TSV-like)
        count += content.Count(c => c == '\t');

        return count;
    }

    private static int CountListPatterns(IEnumerable<string> lines)
    {
        var count = 0;
        foreach (var line in lines)
        {
            try
            {
                // Use pre-compiled regex patterns for performance and security
                if (BulletPointRegex.IsMatch(line) ||
                    NumberedListRegex.IsMatch(line) ||
                    LetterListRegex.IsMatch(line) ||
                    CheckboxRegex.IsMatch(line))
                {
                    count++;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Regex timed out - skip this line to prevent ReDoS
                continue;
            }
        }

        return count;
    }

    private static int CountParagraphBreaks(string content)
    {
        var doubleNewlineCount = 0;
        for (var i = 0; i < content.Length - 1; i++)
        {
            if (content[i] == '\n' && content[i + 1] == '\n')
            {
                doubleNewlineCount++;
                i++; // Skip the second newline
            }
        }
        return doubleNewlineCount;
    }

    private sealed record ContentMetrics
    {
        public double TableDensity { get; init; }
        public int TablePatternCount { get; init; }
        public double ListDensity { get; init; }
        public bool IsNarrative { get; init; }
        public int ParagraphCount { get; init; }
    }
}
