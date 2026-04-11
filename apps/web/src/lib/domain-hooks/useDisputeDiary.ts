/**
 * useDisputeDiary Hook (GAP-006)
 *
 * Registers a dispute resolution as a diary entry (private note) for a
 * game session. Uses the existing `saveNote` API with noteType
 * `'dispute_resolved'` so the backend stores the arbitration decision.
 *
 * @example
 * ```typescript
 * const { createEntry } = useDisputeDiary();
 * await createEntry({ sessionId, question, ruling });
 * ```
 */

import { useCallback } from 'react';

import { api } from '@/lib/api';

export interface DisputeEntryInput {
  /** The game session ID */
  sessionId: string;
  /** The user's question / dispute description */
  question: string;
  /** The AI arbitration ruling */
  ruling: string;
  /** Optional knowledge-base chunk that sourced the ruling */
  sourceChunkId?: string;
}

export interface UseDisputeDiaryReturn {
  /**
   * Creates a `dispute_resolved` note for the given session.
   * Throws on API error.
   */
  createEntry: (entry: DisputeEntryInput) => Promise<void>;
}

/**
 * useDisputeDiary
 *
 * Exposes a single `createEntry` callback that persists a dispute diary entry
 * via `POST /api/v1/game-sessions/{sessionId}/private-notes` with
 * `noteType: 'dispute_resolved'`.
 */
export function useDisputeDiary(): UseDisputeDiaryReturn {
  const createEntry = useCallback(async (entry: DisputeEntryInput): Promise<void> => {
    const { sessionId, question, ruling, sourceChunkId } = entry;

    const content = [
      question ? `**Domanda**: ${question}` : null,
      `**Decisione arbitro**: ${ruling}`,
      sourceChunkId ? `**Fonte**: ${sourceChunkId}` : null,
    ]
      .filter(Boolean)
      .join('\n\n');

    await api.sessionTracking.saveNote(sessionId, {
      content,
      noteType: 'dispute_resolved',
      isHidden: false,
    });
  }, []);

  return { createEntry };
}
