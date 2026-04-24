/**
 * Pure helper functions for `ChatMessageItem[]` collections.
 *
 * These were previously inline `useMemo` computations in
 * `chat-unified/ChatThreadView.tsx`. Extracted to `chat/shared/` so that
 * the slide-over panel (`chat/panel/*`) can share the same derivation
 * logic (Phase 0 Strangler Fig, Task 2).
 *
 * RULES:
 * - Must be pure (no DOM, no refs, no hooks).
 * - Must be stable under identical input (referential equality of output
 *   arrays is NOT required — `useMemo` at the call site handles that).
 * - Must preserve current behavior byte-for-byte; this module is not the
 *   place to "fix" semantics. Any semantic change lands in a separate PR.
 */

import type { Citation, ChatMessageItem } from './types';

/**
 * Flatten every message's `citations` array into a single list, preserving
 * message order. Messages without citations contribute nothing.
 *
 * Mirrors the previous inline expression:
 * ```ts
 * messages.flatMap(m => m.citations ?? [])
 * ```
 */
export function collectCitations(messages: ReadonlyArray<ChatMessageItem>): Citation[] {
  return messages.flatMap(m => m.citations ?? []);
}

/**
 * Get the follow-up suggestion strings attached to the **most recent**
 * assistant message. Returns an empty array if no assistant message is
 * present or if the last assistant message has no follow-up questions.
 *
 * Mirrors the previous inline expression:
 * ```ts
 * [...messages].reverse().find(m => m.role === 'assistant')?.followUpQuestions ?? []
 * ```
 */
export function getSuggestedQuestions(messages: ReadonlyArray<ChatMessageItem>): string[] {
  // `findLast` would be cleaner, but we preserve the `[...].reverse().find(...)`
  // shape to guarantee identical semantics (including the behavior when
  // `followUpQuestions` is an empty array vs undefined — both yield []).
  const lastAssistant = [...messages].reverse().find(m => m.role === 'assistant');
  return lastAssistant?.followUpQuestions ?? [];
}
