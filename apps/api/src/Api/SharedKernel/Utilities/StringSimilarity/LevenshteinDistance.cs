namespace Api.SharedKernel.Utilities.StringSimilarity;

/// <summary>
/// Utility class for calculating Levenshtein edit distance and similarity scores.
/// Issue #4158: Backend - Duplicate Detection Enhancement
/// </summary>
/// <remarks>
/// Levenshtein distance measures the minimum number of single-character edits
/// (insertions, deletions, substitutions) required to transform one string into another.
///
/// Similarity score is normalized to 0-100 percentage:
/// - 100% = identical strings (0 edits)
/// - 0% = completely different (max edits = length of longer string)
///
/// Examples:
/// - "Catan" vs "Catane" → Distance: 1, Similarity: 83%
/// - "Monopoly" vs "Monopoli" → Distance: 1, Similarity: 89%
/// - "Chess" vs "Checkers" → Distance: 5, Similarity: 38%
/// </remarks>
public static class LevenshteinDistance
{
    /// <summary>
    /// Calculates the Levenshtein edit distance between two strings.
    /// </summary>
    /// <param name="source">First string to compare</param>
    /// <param name="target">Second string to compare</param>
    /// <returns>Minimum number of edits required (0 = identical)</returns>
    /// <remarks>
    /// Uses dynamic programming algorithm with O(m*n) time and space complexity.
    /// Case-insensitive comparison for game title matching.
    /// </remarks>
    public static int CalculateDistance(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.IsNullOrWhiteSpace(target) ? 0 : target.Length;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return source.Length;
        }

        // Normalize to lowercase for case-insensitive comparison
        var s1 = source.ToLowerInvariant();
        var s2 = target.ToLowerInvariant();

        var m = s1.Length;
        var n = s2.Length;

        // Optimization: if strings are identical, return 0 immediately
        if (string.Equals(s1, s2, StringComparison.Ordinal))
        {
            return 0;
        }

        // Create distance matrix [m+1][n+1]
        var matrix = new int[m + 1, n + 1];

        // Initialize first column (deletions from source)
        for (var i = 0; i <= m; i++)
        {
            matrix[i, 0] = i;
        }

        // Initialize first row (insertions to source)
        for (var j = 0; j <= n; j++)
        {
            matrix[0, j] = j;
        }

        // Fill matrix using dynamic programming
        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,      // Deletion
                        matrix[i, j - 1] + 1),     // Insertion
                    matrix[i - 1, j - 1] + cost);  // Substitution
            }
        }

        return matrix[m, n];
    }

    /// <summary>
    /// Calculates similarity score as percentage (0-100) based on Levenshtein distance.
    /// </summary>
    /// <param name="source">First string to compare</param>
    /// <param name="target">Second string to compare</param>
    /// <returns>Similarity score: 100 = identical, 0 = completely different</returns>
    /// <remarks>
    /// Formula: similarity = (1 - distance / maxLength) * 100
    /// where maxLength = max(source.Length, target.Length)
    ///
    /// Threshold guidance for game title matching:
    /// - ≥90%: Very likely duplicate (e.g., "Catan" vs "Catane")
    /// - 70-89%: Possible duplicate, needs review (e.g., "Risk" vs "Risky")
    /// - &lt;70%: Unlikely duplicate (e.g., "Chess" vs "Checkers")
    /// </remarks>
    public static double CalculateSimilarityScore(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(target))
        {
            return 100.0; // Both empty = identical
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return 0.0; // One empty = no similarity
        }

        var distance = CalculateDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);

        // Calculate similarity percentage
        var similarity = (1.0 - (double)distance / maxLength) * 100.0;

        // Ensure result is in [0, 100] range
        return Math.Max(0.0, Math.Min(100.0, similarity));
    }
}
