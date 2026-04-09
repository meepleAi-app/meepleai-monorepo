/**
 * Adapter: ChatMessageItem → ChatMessageProps.
 *
 * Bridges the ChatMessageList state shape (ChatMessageItem) with the
 * <ChatMessage> atom's prop shape. See
 * docs/development/chat-message-api-compatibility.md for the field-by-field
 * mapping rationale (Phase A.0 spike report for Issue #292).
 *
 * Mapping strategy: Option ε — partial composition.
 * - Pass role, content, timestamp, feedback state via adapter
 * - Do NOT pass citations (kept in orchestrator as RuleSourceCard to
 *   preserve RAG copyright tier handling — citation types are incompatible)
 * - Do NOT pass confidence (not available in ChatMessageItem)
 * - Do NOT pass agentType (not available in ChatMessageItem)
 * - avatar.fallback hardcoded to 'U' for user messages
 */

import type { ChatMessageProps } from '@/components/ui/meeple/chat-message';
import type { FeedbackValue } from '@/components/ui/meeple/feedback-buttons';

import type { ChatMessageItem } from '../ChatMessageList';

export interface ToChatMessagePropsContext {
  /** Current feedback value for this message (from feedbackMap) */
  feedback: FeedbackValue;
  /** Whether feedback submission is in-flight for this message */
  isFeedbackLoading: boolean;
  /** Whether feedback buttons should be visible (gameId && threadId truthy) */
  showFeedback: boolean;
  /** Curried feedback handler with messageId already bound */
  onFeedbackChange: (value: FeedbackValue, comment?: string) => Promise<void>;
}

export function toChatMessageProps(
  item: ChatMessageItem,
  ctx: ToChatMessagePropsContext
): ChatMessageProps {
  const base: ChatMessageProps = {
    role: item.role,
    content: item.content,
    timestamp: item.timestamp,
    feedback: ctx.feedback,
    isFeedbackLoading: ctx.isFeedbackLoading,
    showFeedback: ctx.showFeedback,
    onFeedbackChange: ctx.onFeedbackChange,
  };

  if (item.role === 'user') {
    base.avatar = { fallback: 'U' };
  }

  // NOTE: citations deliberately NOT passed — kept outside as <RuleSourceCard>
  // NOTE: confidence/agentType not yet available from ChatMessageItem
  // NOTE: isTyping is handled separately via the streaming bubble, not per message

  return base;
}
