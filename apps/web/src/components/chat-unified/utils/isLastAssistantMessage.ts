import type { ChatMessageItem } from '../ChatMessageList';

/**
 * Returns true if the message at `index` is the last assistant message in the list.
 *
 * Used by ChatMessageList to decide which assistant message is the "final" one
 * and should render context-dependent children (strategy tier badge, technical
 * details panel).
 *
 * Note: this helper only checks the message list structure. Callers that also
 * want to gate on streaming state should combine with `!streamState.isStreaming`
 * externally.
 *
 * @example
 * ```ts
 * const messages = [u('u1'), a('a1'), u('u2'), a('a2')];
 * isLastAssistantMessage(messages, 3); // true  (a2 is last)
 * isLastAssistantMessage(messages, 1); // false (a1 is not last)
 * isLastAssistantMessage(messages, 2); // false (user message)
 * ```
 */
export function isLastAssistantMessage(
  messages: ReadonlyArray<ChatMessageItem>,
  index: number
): boolean {
  const msg = messages[index];
  if (!msg || msg.role !== 'assistant') return false;
  // Find the last message with role='assistant' and compare its index
  for (let i = messages.length - 1; i >= 0; i--) {
    if (messages[i].role === 'assistant') {
      return i === index;
    }
  }
  return false;
}
