'use client';

/**
 * useChatDraft — Issue #2375 G3.
 *
 * sessionStorage-backed draft persistence per session. Allows the chat input
 * to survive collapse/expand cycles of the ChatAgentPanel (§5 contract:
 * body unmounts when collapsed=true).
 *
 * Key format: `meepleai.chat-draft.${sessionId}`.
 *
 * - SSR-safe: returns empty string when `window` is undefined.
 * - sessionId=null → all operations no-op (no sessionStorage access).
 * - Quota exceeded / sessionStorage unavailable (Safari private mode) →
 *   console.warn + swallow. Component stays functional with no persistence.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2375-g3-chatagent-always-visible-design.md §4.2
 */

import { useCallback, useEffect, useState } from 'react';

export const CHAT_DRAFT_KEY_PREFIX = 'meepleai.chat-draft.';

export interface UseChatDraftOptions {
  readonly sessionId: string | null;
}

export interface UseChatDraftReturn {
  readonly draft: string;
  readonly setDraft: (next: string) => void;
  readonly clearDraft: () => void;
}

function readDraft(sessionId: string | null): string {
  if (sessionId == null) return '';
  if (typeof window === 'undefined') return '';
  try {
    return window.sessionStorage.getItem(`${CHAT_DRAFT_KEY_PREFIX}${sessionId}`) ?? '';
  } catch (err) {
    console.warn('[useChatDraft] sessionStorage.getItem failed:', err);
    return '';
  }
}

export function useChatDraft({ sessionId }: UseChatDraftOptions): UseChatDraftReturn {
  const [draft, setDraftState] = useState<string>(() => readDraft(sessionId));

  // I1 fix: re-read from storage when sessionId transitions null → string (post-hydration).
  useEffect(() => {
    if (sessionId == null) return;
    const stored = readDraft(sessionId);
    if (stored) setDraftState(stored);
  }, [sessionId]);

  const setDraft = useCallback(
    (next: string) => {
      if (sessionId == null) return;
      setDraftState(next);
      if (typeof window === 'undefined') return;
      try {
        window.sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}${sessionId}`, next);
      } catch (err) {
        console.warn('[useChatDraft] sessionStorage.setItem failed:', err);
      }
    },
    [sessionId]
  );

  const clearDraft = useCallback(() => {
    if (sessionId == null) return; // C1 fix: guard FIRST, mirroring setDraft
    setDraftState('');
    if (typeof window === 'undefined') return;
    try {
      window.sessionStorage.removeItem(`${CHAT_DRAFT_KEY_PREFIX}${sessionId}`);
    } catch (err) {
      console.warn('[useChatDraft] sessionStorage.removeItem failed:', err);
    }
  }, [sessionId]);

  return { draft, setDraft, clearDraft };
}
