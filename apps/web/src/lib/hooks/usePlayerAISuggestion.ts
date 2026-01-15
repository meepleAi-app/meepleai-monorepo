import { useCallback, useState } from 'react';

import { createApiClient } from '@/lib/api';
import type { PlayerModeSuggestionResponse, SuggestedMove } from '@/lib/api/schemas';

/**
 * Hook state for Player Mode AI suggestions
 * Issue #2421: Player Mode UI Controls
 */
export interface PlayerAISuggestionState {
  isLoading: boolean;
  error: string | null;
  suggestion: SuggestedMove | null;
  alternatives: SuggestedMove[];
  confidence: number | null;
  strategicContext: string | null;
  processingTimeMs: number | null;
}

/**
 * Hook controls for Player Mode AI suggestions
 */
export interface PlayerAISuggestionControls {
  suggestMove: (gameId: string, gameState: Record<string, any>, query?: string) => Promise<void>;
  applySuggestion: () => void;
  ignoreSuggestion: () => void;
  reset: () => void;
}

const INITIAL_STATE: PlayerAISuggestionState = {
  isLoading: false,
  error: null,
  suggestion: null,
  alternatives: [],
  confidence: null,
  strategicContext: null,
  processingTimeMs: null,
};

/**
 * React hook for requesting AI move suggestions in Player Mode
 *
 * Issue #2421: Player Mode UI Controls
 *
 * Usage:
 * ```tsx
 * const [suggestionState, suggestionControls] = usePlayerAISuggestion({
 *   onSuggestionReceived: (suggestion, confidence) => {
 *     console.log('Got suggestion:', suggestion);
 *   },
 *   onSuggestionApplied: (suggestion) => {
 *     // Update game state with suggested move
 *   },
 *   onError: (error) => {
 *     console.error('Suggestion failed:', error);
 *   }
 * });
 *
 * // Request a suggestion
 * await suggestionControls.suggestMove(gameId, currentGameState, 'Should I focus on resources?');
 *
 * // Apply the suggestion
 * suggestionControls.applySuggestion();
 * ```
 *
 * @param callbacks - Optional callbacks for lifecycle events
 * @returns [suggestionState, suggestionControls] - Current state and control functions
 */
export function usePlayerAISuggestion(callbacks?: {
  onSuggestionReceived?: (suggestion: SuggestedMove, confidence: number) => void;
  onSuggestionApplied?: (suggestion: SuggestedMove) => void;
  onSuggestionIgnored?: () => void;
  onError?: (error: string) => void;
}): [PlayerAISuggestionState, PlayerAISuggestionControls] {
  const [state, setState] = useState<PlayerAISuggestionState>(INITIAL_STATE);

  const reset = useCallback(() => {
    setState(INITIAL_STATE);
  }, []);

  const suggestMove = useCallback(
    async (gameId: string, gameState: Record<string, any>, query?: string) => {
      // Reset state and start loading
      setState({
        ...INITIAL_STATE,
        isLoading: true,
      });

      try {
        // Create API client
        const apiClient = createApiClient();

        // Call suggestPlayerMove endpoint
        const response: PlayerModeSuggestionResponse = await apiClient.agents.suggestPlayerMove({
          gameId,
          gameState,
          query,
        });

        // Update state with response
        const newState: PlayerAISuggestionState = {
          isLoading: false,
          error: null,
          suggestion: response.primarySuggestion,
          alternatives: response.alternativeMoves || [],
          confidence: response.overallConfidence,
          strategicContext: response.strategicContext || null,
          processingTimeMs: response.processingTimeMs,
        };

        setState(newState);

        // Call callback
        if (callbacks?.onSuggestionReceived) {
          callbacks.onSuggestionReceived(response.primarySuggestion, response.overallConfidence);
        }
      } catch (error) {
        const errorMessage =
          error instanceof Error
            ? error.message
            : 'Failed to get AI suggestion. The backend endpoint may not be implemented yet.';

        setState(prev => ({
          ...prev,
          error: errorMessage,
          isLoading: false,
        }));

        if (callbacks?.onError) {
          callbacks.onError(errorMessage);
        }
      }
    },
    [callbacks]
  );

  const applySuggestion = useCallback(() => {
    if (state.suggestion && callbacks?.onSuggestionApplied) {
      callbacks.onSuggestionApplied(state.suggestion);
    }
    // Reset after applying
    reset();
  }, [state.suggestion, callbacks, reset]);

  const ignoreSuggestion = useCallback(() => {
    if (callbacks?.onSuggestionIgnored) {
      callbacks.onSuggestionIgnored();
    }
    // Reset after ignoring
    reset();
  }, [callbacks, reset]);

  return [
    state,
    {
      suggestMove,
      applySuggestion,
      ignoreSuggestion,
      reset,
    },
  ];
}
