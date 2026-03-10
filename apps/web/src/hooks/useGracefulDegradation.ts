/**
 * useGracefulDegradation Hook (Issue #5586)
 *
 * Detects LLM unavailability (SSE errors, circuit breaker, network issues)
 * and provides automatic fallback to cached FAQ search.
 *
 * Response sources:
 * - 'live': Normal AI response from backend
 * - 'cached': Answer from locally cached FAQ
 * - 'offline': No match found, suggest manual PDF lookup
 *
 * @module hooks/useGracefulDegradation
 *
 * @example
 * ```tsx
 * function ChatView({ gameId }: { gameId: string }) {
 *   const { tryFallback, responseSource, isLlmAvailable } = useGracefulDegradation(gameId);
 *
 *   // On SSE error, try fallback
 *   const handleError = (error: string) => {
 *     const fallback = tryFallback(userMessage);
 *     if (fallback) showCachedResponse(fallback);
 *   };
 * }
 * ```
 */

import { useCallback, useState } from 'react';

import { searchFaqs, searchCommonQuestions, suggestSection } from '@/lib/game-night/faq-search';
import type { FaqSearchResult } from '@/lib/game-night/faq-search';
import { getCachedAnalyses } from '@/lib/game-night/rules-cache';

// ============================================================================
// Types
// ============================================================================

/** Where the response came from */
export type ResponseSource = 'live' | 'cached' | 'offline';

export interface FallbackResponse {
  /** The answer text */
  answer: string;
  /** Source of the answer */
  source: ResponseSource;
  /** The original FAQ match (if source is 'cached') */
  faqMatch?: FaqSearchResult;
  /** Suggested PDF section to look at (if source is 'offline') */
  suggestedSection?: string | null;
  /** Confidence of the match (0-1) */
  confidence: number;
}

export interface UseGracefulDegradationReturn {
  /** Whether the LLM appears available (no recent errors) */
  isLlmAvailable: boolean;
  /** Number of consecutive LLM errors */
  consecutiveErrors: number;
  /** Try to find a cached FAQ answer for the query */
  tryFallback: (query: string) => FallbackResponse | null;
  /** Record a successful LLM response (resets error count) */
  recordSuccess: () => void;
  /** Record an LLM error (increments error count) */
  recordError: () => void;
  /** The source of the most recent response */
  lastResponseSource: ResponseSource;
  /** Whether circuit breaker is tripped (3+ consecutive errors) */
  isCircuitBroken: boolean;
}

// ============================================================================
// Constants
// ============================================================================

/** Number of consecutive errors before considering LLM unavailable */
const ERROR_THRESHOLD = 2;

/** Number of consecutive errors before circuit breaker trips */
const CIRCUIT_BREAKER_THRESHOLD = 3;

// ============================================================================
// Hook
// ============================================================================

export function useGracefulDegradation(gameId: string | null): UseGracefulDegradationReturn {
  const [consecutiveErrors, setConsecutiveErrors] = useState(0);
  const [lastResponseSource, setLastResponseSource] = useState<ResponseSource>('live');

  const isLlmAvailable = consecutiveErrors < ERROR_THRESHOLD;
  const isCircuitBroken = consecutiveErrors >= CIRCUIT_BREAKER_THRESHOLD;

  const recordSuccess = useCallback(() => {
    setConsecutiveErrors(0);
    setLastResponseSource('live');
  }, []);

  const recordError = useCallback(() => {
    setConsecutiveErrors(prev => prev + 1);
  }, []);

  const tryFallback = useCallback(
    (query: string): FallbackResponse | null => {
      if (!gameId) return null;

      const cached = getCachedAnalyses(gameId);
      if (!cached || cached.analyses.length === 0) {
        setLastResponseSource('offline');
        return {
          answer:
            'No cached rules available for this game. Please check the PDF rulebook directly.',
          source: 'offline',
          suggestedSection: null,
          confidence: 0,
        };
      }

      // Aggregate all FAQs and common questions from all analyses
      const allFaqs = cached.analyses.flatMap(a => a.generatedFaqs);
      const allCommonQuestions = cached.analyses.flatMap(a => a.commonQuestions);
      const allPhases = cached.analyses.flatMap(a => a.gamePhases);

      // Search generated FAQs first (have full Q&A pairs)
      const faqResults = searchFaqs(query, allFaqs, { maxResults: 1, minScore: 0.2 });

      if (faqResults.length > 0) {
        const bestMatch = faqResults[0];
        setLastResponseSource('cached');
        return {
          answer: bestMatch.faq.answer,
          source: 'cached',
          faqMatch: bestMatch,
          confidence: bestMatch.score,
        };
      }

      // Try common questions (string-only, no answers)
      const commonResults = searchCommonQuestions(query, allCommonQuestions, 1);
      if (commonResults.length > 0) {
        setLastResponseSource('cached');
        return {
          answer: `Related question found: "${commonResults[0].question}". Check the rulebook for the detailed answer.`,
          source: 'cached',
          confidence: commonResults[0].score * 0.7, // Lower confidence since no answer
        };
      }

      // No match - suggest a section
      const section = suggestSection(query, allPhases);
      setLastResponseSource('offline');
      return {
        answer: section
          ? `No cached answer found. Try looking in the "${section}" section of the rulebook.`
          : 'No cached answer found. Please search manually in the PDF rulebook.',
        source: 'offline',
        suggestedSection: section,
        confidence: 0,
      };
    },
    [gameId]
  );

  return {
    isLlmAvailable,
    consecutiveErrors,
    tryFallback,
    recordSuccess,
    recordError,
    lastResponseSource,
    isCircuitBroken,
  };
}
