using Api.Services.Pdf;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for converting PDF tables to atomic game rules
/// Pure business logic without infrastructure dependencies
/// </summary>
/// <remarks>
/// This service contains the BUSINESS RULES for interpreting table data:
/// - Header row detection and validation
/// - Key-value pair extraction from table cells
/// - Atomic rule creation with proper formatting
/// - Rule categorization and structuring
///
/// Why this is Domain Logic (not Infrastructure):
/// - Defines WHAT atomic rules mean in the game rules domain
/// - Encapsulates business invariants (format, structure, validation)
/// - Independent of PDF parsing technology
/// - Testable without external dependencies
/// </remarks>
public class TableToAtomicRuleConverter
{
    /// <summary>
    /// Converts a PDF table to a list of atomic game rules
    /// </summary>
    /// <param name="table">The PDF table to convert</param>
    /// <returns>List of atomic rules extracted from the table</returns>
    /// <remarks>
    /// Business Rules Applied:
    /// 1. Empty tables produce no rules (guard clause)
    /// 2. Each table row becomes one atomic rule (1:1 mapping)
    /// 3. Rule format: "[Table on page X] Header1: Value1; Header2: Value2"
    /// 4. Empty cells are skipped (no null/empty values in rules)
    /// 5. Page number is always included for traceability
    /// </remarks>
    public List<string> ConvertTableToAtomicRules(PdfTable table)
    {
        var rules = new List<string>();

        // Guard: Empty tables cannot produce rules
        if (table.Headers.Count == 0 || table.Rows.Count == 0)
        {
            return rules;
        }

        // Business Logic: Each row represents one atomic rule in the game
        foreach (var row in table.Rows)
        {
            var ruleParts = ExtractRulePartsFromRow(table.Headers, row);

            if (ruleParts.Count > 0)
            {
                var atomicRule = FormatAtomicRule(table.PageNumber, ruleParts);
                rules.Add(atomicRule);
            }
        }

        return rules;
    }

    /// <summary>
    /// Extracts rule parts from a table row by matching headers to cell values
    /// </summary>
    /// <param name="headers">Table column headers</param>
    /// <param name="row">Table row data</param>
    /// <returns>List of "Header: Value" pairs</returns>
    private static List<string> ExtractRulePartsFromRow(List<string> headers, string[] row)
    {
        var parts = new List<string>();
        var columnCount = Math.Min(headers.Count, row.Length);

        for (int i = 0; i < columnCount; i++)
        {
            var value = row[i];
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Business Rule: Format as "Header: Value" for clarity
                parts.Add($"{headers[i]}: {value}");
            }
        }

        return parts;
    }

    /// <summary>
    /// Formats an atomic rule with page number and rule parts
    /// </summary>
    /// <param name="pageNumber">Page number where the table was found</param>
    /// <param name="ruleParts">List of "Header: Value" pairs</param>
    /// <returns>Formatted atomic rule string</returns>
    private static string FormatAtomicRule(int pageNumber, List<string> ruleParts)
    {
        // Business Rule: Always include page number for traceability
        // Format: [Table on page N] Part1; Part2; Part3
        return $"[Table on page {pageNumber}] {string.Join("; ", ruleParts)}";
    }

    /// <summary>
    /// Validates if a row represents a header row (business logic for header detection)
    /// </summary>
    /// <param name="row">Table row to validate</param>
    /// <returns>True if the row is a header row</returns>
    /// <remarks>
    /// Business Rule for Header Detection:
    /// - Contains "Header" keyword (case-insensitive)
    /// - OR contains typical header terms like "Name", "Type", "Description"
    /// - OR all cells are short and capitalized
    ///
    /// This is BUSINESS LOGIC because it defines what constitutes a
    /// meaningful header in game rule tables, not how to parse PDF data.
    /// </remarks>
    public bool IsHeaderRow(string[] row)
    {
        if (row == null || row.Length == 0)
        {
            return false;
        }

        // Business Rule 1: Explicit header marker
        if (row.Any(cell => cell.Contains("Header", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Business Rule 2: Common header terms in game rules
        var commonHeaderTerms = new[] { "Name", "Type", "Description", "Action", "Effect", "Condition", "Value", "Cost" };
        var headerTermCount = row.Count(cell =>
            commonHeaderTerms.Any(term => cell.Contains(term, StringComparison.OrdinalIgnoreCase)));

        if (headerTermCount >= 2) // At least 2 header terms
        {
            return true;
        }

        // Business Rule 3: All cells are short and likely capitalized (typical header style)
        // Headers are typically < 30 chars (not long descriptions)
        var allShortAndCapitalized = row.All(cell =>
            !string.IsNullOrWhiteSpace(cell) &&
            cell.Length < 30 &&  // Strict length limit for headers
            char.IsUpper(cell.TrimStart()[0]));

        return allShortAndCapitalized;
    }

    /// <summary>
    /// Categorizes an atomic rule based on its content (business domain classification)
    /// </summary>
    /// <param name="atomicRule">The atomic rule to categorize</param>
    /// <returns>Category name (e.g., "Setup", "Action", "Scoring")</returns>
    /// <remarks>
    /// This is BUSINESS LOGIC because it defines the game rule taxonomy,
    /// not the technical mechanics of rule storage or retrieval.
    ///
    /// Categories map to game phases and rule types understood by game designers.
    /// </remarks>
    public string CategorizeAtomicRule(string atomicRule)
    {
        if (string.IsNullOrWhiteSpace(atomicRule))
        {
            return "Unknown";
        }

        var lowerRule = atomicRule.ToLowerInvariant();

        // Business Domain Categories for Game Rules
        if (lowerRule.Contains("setup") || lowerRule.Contains("start") || lowerRule.Contains("initial"))
        {
            return "Setup";
        }

        if (lowerRule.Contains("action") || lowerRule.Contains("turn") || lowerRule.Contains("move"))
        {
            return "Action";
        }

        if (lowerRule.Contains("score") || lowerRule.Contains("point") || lowerRule.Contains("victory"))
        {
            return "Scoring";
        }

        if (lowerRule.Contains("end") || lowerRule.Contains("game over") || lowerRule.Contains("win"))
        {
            return "EndGame";
        }

        if (lowerRule.Contains("component") || lowerRule.Contains("card") || lowerRule.Contains("token"))
        {
            return "Components";
        }

        return "General";
    }
}
