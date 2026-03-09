/**
 * Fuzzy FAQ Search (Issue #5586)
 *
 * Simple fuzzy matching against cached FAQ entries from RulebookAnalysis.
 * Used for offline/degraded mode to provide instant answers from cache.
 *
 * Scoring:
 * - Exact substring match in question: high score
 * - Word overlap between query and question/answer: medium score
 * - Tag match: bonus points
 *
 * @module lib/game-night/faq-search
 */

import type { GeneratedFaqDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Types
// ============================================================================

export interface FaqSearchResult {
  /** The matched FAQ entry */
  faq: GeneratedFaqDto;
  /** Relevance score (0-1, higher is better) */
  score: number;
  /** Which parts matched (for highlighting) */
  matchType: 'exact' | 'partial' | 'tag';
}

export interface FaqSearchOptions {
  /** Maximum number of results to return (default: 3) */
  maxResults?: number;
  /** Minimum score threshold (default: 0.15) */
  minScore?: number;
}

// ============================================================================
// Text Processing Helpers
// ============================================================================

/**
 * Normalize text for comparison: lowercase, strip punctuation, collapse whitespace.
 */
function normalize(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^\w\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Extract unique meaningful words (length >= 3) from text.
 */
function extractWords(text: string): Set<string> {
  const normalized = normalize(text);
  const words = normalized.split(' ').filter(w => w.length >= 3);
  return new Set(words);
}

/**
 * Calculate Jaccard similarity between two word sets.
 */
function jaccardSimilarity(setA: Set<string>, setB: Set<string>): number {
  if (setA.size === 0 && setB.size === 0) return 0;

  let intersection = 0;
  for (const word of setA) {
    if (setB.has(word)) intersection++;
  }

  const union = setA.size + setB.size - intersection;
  return union === 0 ? 0 : intersection / union;
}

// ============================================================================
// Scoring
// ============================================================================

/**
 * Score a single FAQ entry against a query.
 */
function scoreFaq(
  query: string,
  faq: GeneratedFaqDto
): { score: number; matchType: FaqSearchResult['matchType'] } {
  const normalizedQuery = normalize(query);
  const normalizedQuestion = normalize(faq.question);
  const normalizedAnswer = normalize(faq.answer);

  let score = 0;
  let matchType: FaqSearchResult['matchType'] = 'partial';

  // 1. Exact substring match in question (highest weight)
  if (normalizedQuestion.includes(normalizedQuery)) {
    score += 0.6;
    matchType = 'exact';
  } else if (normalizedAnswer.includes(normalizedQuery)) {
    score += 0.35;
    matchType = 'exact';
  }

  // 2. Word overlap scoring
  const queryWords = extractWords(query);
  const questionWords = extractWords(faq.question);
  const answerWords = extractWords(faq.answer);

  const questionSimilarity = jaccardSimilarity(queryWords, questionWords);
  const answerSimilarity = jaccardSimilarity(queryWords, answerWords);

  // Question match is weighted higher than answer match
  score += questionSimilarity * 0.3;
  score += answerSimilarity * 0.1;

  // 3. Tag match bonus
  const queryLower = query.toLowerCase();
  const tagMatch = faq.tags.some(tag => queryLower.includes(tag.toLowerCase()));
  if (tagMatch) {
    score += 0.1;
    if (matchType === 'partial' && score < 0.2) {
      matchType = 'tag';
    }
  }

  // 4. Confidence weighting (higher confidence FAQs are preferred)
  score *= 0.8 + faq.confidence * 0.2;

  // Clamp to [0, 1]
  score = Math.min(1, Math.max(0, score));

  return { score, matchType };
}

// ============================================================================
// Search Function
// ============================================================================

/**
 * Search through FAQ entries for matches to a user query.
 *
 * @param query - The user's question/search text
 * @param faqs - Array of GeneratedFaqDto from cached RulebookAnalysis
 * @param options - Search configuration
 * @returns Ranked list of matching FAQs
 */
export function searchFaqs(
  query: string,
  faqs: GeneratedFaqDto[],
  options: FaqSearchOptions = {}
): FaqSearchResult[] {
  const { maxResults = 3, minScore = 0.15 } = options;

  if (!query.trim() || faqs.length === 0) {
    return [];
  }

  const scored: FaqSearchResult[] = faqs
    .map(faq => {
      const { score, matchType } = scoreFaq(query, faq);
      return { faq, score, matchType };
    })
    .filter(result => result.score >= minScore)
    .sort((a, b) => b.score - a.score)
    .slice(0, maxResults);

  return scored;
}

/**
 * Search through common questions (simple string array) for matches.
 * Returns matched questions with scores.
 */
export function searchCommonQuestions(
  query: string,
  commonQuestions: string[],
  maxResults: number = 3
): Array<{ question: string; score: number }> {
  if (!query.trim() || commonQuestions.length === 0) {
    return [];
  }

  const normalizedQuery = normalize(query);
  const queryWords = extractWords(query);

  return commonQuestions
    .map(question => {
      const normalizedQ = normalize(question);
      let score = 0;

      // Substring match
      if (normalizedQ.includes(normalizedQuery)) {
        score += 0.6;
      }

      // Word overlap
      const qWords = extractWords(question);
      score += jaccardSimilarity(queryWords, qWords) * 0.4;

      return { question, score };
    })
    .filter(r => r.score >= 0.15)
    .sort((a, b) => b.score - a.score)
    .slice(0, maxResults);
}

/**
 * Suggest a relevant PDF section based on the query and game phases.
 * Used when no FAQ match is found to guide the user.
 */
export function suggestSection(
  query: string,
  gamePhases: Array<{ name: string; description: string; order: number }>
): string | null {
  if (!query.trim() || gamePhases.length === 0) return null;

  const queryWords = extractWords(query);
  let bestMatch: { name: string; score: number } | null = null;

  for (const phase of gamePhases) {
    const phaseWords = new Set([...extractWords(phase.name), ...extractWords(phase.description)]);

    const score = jaccardSimilarity(queryWords, phaseWords);
    if (score > 0.1 && (!bestMatch || score > bestMatch.score)) {
      bestMatch = { name: phase.name, score };
    }
  }

  return bestMatch?.name ?? null;
}
