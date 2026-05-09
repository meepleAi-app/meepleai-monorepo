/**
 * v2 game-chat barrel — exports per il flusso "serata di gioco" (G1+G5).
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md
 */

export { CitationChip } from './CitationChip';
export type { CitationChipProps } from './CitationChip';

export { CitationModal } from './CitationModal';
export type { CitationModalProps } from './CitationModal';

export { ConfidenceBadge, getConfidenceTier } from './ConfidenceBadge';
export type { ConfidenceBadgeProps, ConfidenceTier } from './ConfidenceBadge';

export { LowConfidenceDisclaimer } from './LowConfidenceDisclaimer';
export type {
  LowConfidenceDisclaimerProps,
  DisclaimerAlternative,
} from './LowConfidenceDisclaimer';

export { OutOfContextActions } from './OutOfContextActions';
export type {
  OutOfContextActionsProps,
  OutOfContextAction,
  OutOfContextActionKind,
} from './OutOfContextActions';

export { TypingIndicator } from './TypingIndicator';
export type { TypingIndicatorProps } from './TypingIndicator';

export { ChatBubble } from './ChatBubble';
export type { ChatBubbleProps } from './ChatBubble';

export { SuggestedPrompts } from './SuggestedPrompts';
export type { SuggestedPromptsProps, SuggestedPrompt, PromptCategory } from './SuggestedPrompts';

export { ChatInputBar } from './ChatInputBar';
export type { ChatInputBarProps } from './ChatInputBar';

export { GameChatHeader } from './GameChatHeader';
export type { GameChatHeaderProps, AgentKind } from './GameChatHeader';

export { GameChatSidebar } from './GameChatSidebar';
export type { GameChatSidebarProps, ChatHistoryItem } from './GameChatSidebar';
