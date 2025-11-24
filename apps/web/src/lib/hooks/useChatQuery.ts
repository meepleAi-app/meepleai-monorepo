import { useCallback, useState } from 'react';
import type { Citation } from '@/types';

type Snippet = {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
};

export type QueryState = {
  state: string | null;
  currentAnswer: string;
  snippets: Snippet[];
  citations: Citation[];
  confidence: number | null;
  isLoading: boolean;
  error: string | null;
};

export type QueryControls = {
  askQuestion: (gameId: string, query: string) => Promise<void>;
  reset: () => void;
};

const INITIAL_STATE: QueryState = {
  state: null,
  currentAnswer: '',
  snippets: [],
  citations: [],
  confidence: null,
  isLoading: false,
  error: null,
};

/**
 * React hook for asking questions to the Board Game AI backend
 *
 * Issue #1006: Backend API Integration (/api/v1/knowledge-base/ask)
 *
 * Usage:
 * ```tsx
 * const [queryState, queryControls] = useChatQuery({
 *   onComplete: (answer, citations, metadata) => {
 *     // Handle completed response
 *   },
 *   onError: (error) => {
 *     // Handle error
 *   }
 * });
 *
 * // Ask a question
 * await queryControls.askQuestion(gameId, userQuery);
 * ```
 *
 * @param callbacks - Optional callbacks for completion and error handling
 * @returns [queryState, queryControls] - Current state and control functions
 */
export function useChatQuery(callbacks?: {
  onComplete?: (answer: string, citations: Citation[], metadata: { confidence: number | null }) => void;
  onError?: (error: string) => void;
}): [QueryState, QueryControls] {
  const [state, setState] = useState<QueryState>(INITIAL_STATE);

  const reset = useCallback(() => {
    setState(INITIAL_STATE);
  }, []);

  const askQuestion = useCallback(
    async (gameId: string, query: string) => {
      // Reset state and start loading
      setState({
        ...INITIAL_STATE,
        isLoading: true,
        state: 'Searching knowledge base...',
      });

      try {
        // Build URL
        const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
        const url = new URL('/api/v1/knowledge-base/ask', baseUrl);

        // Make POST request
        const response = await fetch(url.toString(), {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            gameId,
            query,
            language: 'en',
            bypassCache: false,
          }),
          credentials: 'include', // Include cookies for authentication
        });

        if (!response.ok) {
          let errorMessage = `HTTP error! status: ${response.status}`;
          try {
            const errorData = await response.json();
            errorMessage = errorData.error || errorData.message || errorMessage;
          } catch {
            // Use default error message if JSON parsing fails
          }
          throw new Error(errorMessage);
        }

        // Parse response
        const data = await response.json();

        // Check for success field
        if (!data.success) {
          throw new Error(data.error || 'Request failed');
        }

        // Map backend response to state
        // Backend returns: { success, answer, sources, citations, overallConfidence, ... }
        const mappedCitations: Citation[] = (data.citations || []).map((citation: any) => ({
          documentId: citation.documentId || citation.source || 'Unknown',
          pageNumber: citation.pageNumber || citation.page || 0,
          snippet: citation.snippet || citation.text || '',
          relevanceScore: citation.relevanceScore || citation.score || null,
        }));

        const finalState: QueryState = {
          state: null,
          currentAnswer: data.answer || '',
          snippets: data.sources || [],
          citations: mappedCitations,
          confidence: data.overallConfidence || null,
          isLoading: false,
          error: null,
        };

        setState(finalState);

        // Call completion callback
        if (callbacks?.onComplete) {
          callbacks.onComplete(finalState.currentAnswer, finalState.citations, {
            confidence: finalState.confidence,
          });
        }
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Failed to get answer';
        setState((prev) => ({
          ...prev,
          error: errorMessage,
          isLoading: false,
          state: null,
        }));

        if (callbacks?.onError) {
          callbacks.onError(errorMessage);
        }
      }
    },
    [callbacks]
  );

  return [
    state,
    {
      askQuestion,
      reset,
    },
  ];
}
