/**
 * String Similarity Utilities
 * Issue #4164: BGG Match Score Calculation
 *
 * Provides string similarity algorithms for fuzzy matching.
 * Used for calculating match percentage between extracted game titles and BGG results.
 */

/**
 * Calculate Levenshtein distance between two strings
 * @param a First string
 * @param b Second string
 * @returns Number of edits required to transform a into b
 */
function levenshteinDistance(a: string, b: string): number {
  const matrix: number[][] = [];

  // Initialize first column (deletion costs)
  for (let i = 0; i <= b.length; i++) {
    matrix[i] = [i];
  }

  // Initialize first row (insertion costs)
  for (let j = 0; j <= a.length; j++) {
    matrix[0][j] = j;
  }

  // Fill matrix
  for (let i = 1; i <= b.length; i++) {
    for (let j = 1; j <= a.length; j++) {
      if (b.charAt(i - 1) === a.charAt(j - 1)) {
        // Characters match, no cost
        matrix[i][j] = matrix[i - 1][j - 1];
      } else {
        // Take minimum of: substitution, insertion, deletion
        matrix[i][j] = Math.min(
          matrix[i - 1][j - 1] + 1, // substitution
          matrix[i][j - 1] + 1, // insertion
          matrix[i - 1][j] + 1 // deletion
        );
      }
    }
  }

  return matrix[b.length][a.length];
}

/**
 * Calculate similarity percentage between two strings
 * @param str1 First string to compare
 * @param str2 Second string to compare
 * @returns Similarity percentage (0-100)
 *
 * @example
 * calculateStringSimilarity('Catan', 'Catan') // 100
 * calculateStringSimilarity('Catan', 'Settlers of Catan') // 71
 * calculateStringSimilarity('Chess', 'Checkers') // 57
 */
export function calculateStringSimilarity(str1: string, str2: string): number {
  // Normalize strings (lowercase, trim)
  const a = str1.toLowerCase().trim();
  const b = str2.toLowerCase().trim();

  // Empty strings (check before exact match to avoid '' === '' returning 100)
  if (a.length === 0 || b.length === 0) return 0;

  // Exact match
  if (a === b) return 100;

  // Calculate Levenshtein distance
  const distance = levenshteinDistance(a, b);
  const maxLength = Math.max(a.length, b.length);

  // Convert to percentage (0-100)
  const similarity = (1 - distance / maxLength) * 100;

  return Math.round(similarity);
}

/**
 * Check if two strings are similar above a threshold
 * @param str1 First string
 * @param str2 Second string
 * @param threshold Minimum similarity percentage (0-100, default: 70)
 * @returns True if similarity >= threshold
 */
export function isSimilar(str1: string, str2: string, threshold: number = 70): boolean {
  return calculateStringSimilarity(str1, str2) >= threshold;
}
