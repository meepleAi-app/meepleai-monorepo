/**
 * `useChatScroll` — scroll an anchor element into view whenever any of the
 * provided dependencies changes.
 *
 * Extracted from `chat-unified/ChatThreadView.tsx` (Phase 0 Strangler Fig,
 * Task 3). The original inline logic was:
 *
 * ```ts
 * const messagesEndRef = useRef<HTMLDivElement>(null);
 * const scrollToBottom = useCallback(() => {
 *   messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
 * }, []);
 * useEffect(() => {
 *   scrollToBottom();
 * }, [messages, streamState.currentAnswer, scrollToBottom]);
 * ```
 *
 * The plan's originally proposed signature was `useChatScroll(messages)` but
 * the real inline effect also re-scrolls on streaming-text updates
 * (`streamState.currentAnswer`). We therefore accept a generic dependency
 * array so callers retain full control of scroll triggers.
 *
 * RULES:
 * - No refactor. Behavior matches the inline implementation byte-for-byte:
 *   same `scrollIntoView({ behavior: 'smooth' })`, same scroll-on-every-dep-change.
 * - No SSR branch. `scrollIntoView` is guarded by the ref's optional chain.
 */

import { useCallback, useEffect, useRef } from 'react';

export interface UseChatScrollResult<T extends HTMLElement> {
  anchorRef: React.RefObject<T | null>;
  scrollToBottom: () => void;
}

export function useChatScroll<T extends HTMLElement = HTMLDivElement>(
  triggers: ReadonlyArray<unknown>
): UseChatScrollResult<T> {
  const anchorRef = useRef<T | null>(null);

  const scrollToBottom = useCallback(() => {
    anchorRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- callers pass trigger list explicitly
  }, [...triggers, scrollToBottom]);

  return { anchorRef, scrollToBottom };
}
