/**
 * v2 game-chat barrel — exports per il flusso "serata di gioco" (G1+G5).
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md
 */

export { CitationChip } from '@/components/features/game-chat/CitationChip';
export type { CitationChipProps } from '@/components/features/game-chat/CitationChip';

export { CitationModal } from '@/components/features/game-chat/CitationModal';
export type { CitationModalProps } from '@/components/features/game-chat/CitationModal';

export {
  ConfidenceBadge,
  getConfidenceTier,
} from '@/components/features/game-chat/ConfidenceBadge';
export type {
  ConfidenceBadgeProps,
  ConfidenceTier,
} from '@/components/features/game-chat/ConfidenceBadge';

export { LowConfidenceDisclaimer } from '@/components/features/game-chat/LowConfidenceDisclaimer';
export type {
  LowConfidenceDisclaimerProps,
  DisclaimerAlternative,
} from '@/components/features/game-chat/LowConfidenceDisclaimer';

export { OutOfContextActions } from '@/components/features/game-chat/OutOfContextActions';
export type {
  OutOfContextActionsProps,
  OutOfContextAction,
  OutOfContextActionKind,
} from '@/components/features/game-chat/OutOfContextActions';

export { TypingIndicator } from '@/components/features/game-chat/TypingIndicator';
export type { TypingIndicatorProps } from '@/components/features/game-chat/TypingIndicator';

export { ChatBubble } from '@/components/features/game-chat/ChatBubble';
export type { ChatBubbleProps } from '@/components/features/game-chat/ChatBubble';

export { SuggestedPrompts } from '@/components/features/game-chat/SuggestedPrompts';
export type {
  SuggestedPromptsProps,
  SuggestedPrompt,
  PromptCategory,
} from '@/components/features/game-chat/SuggestedPrompts';

export { ChatInputBar } from '@/components/features/game-chat/ChatInputBar';
export type { ChatInputBarProps } from '@/components/features/game-chat/ChatInputBar';

export { GameChatHeader } from '@/components/features/game-chat/GameChatHeader';
export type {
  GameChatHeaderProps,
  AgentKind,
} from '@/components/features/game-chat/GameChatHeader';

export { GameChatSidebar } from '@/components/features/game-chat/GameChatSidebar';
export type {
  GameChatSidebarProps,
  ChatHistoryItem,
} from '@/components/features/game-chat/GameChatSidebar';

export { GameChatTab } from '@/components/features/game-chat/GameChatTab';
export type { GameChatTabProps } from '@/components/features/game-chat/GameChatTab';

// G2 fast-resume components
export { ChatBubbleSkeleton } from '@/components/features/game-chat/ChatBubbleSkeleton';
export type { ChatBubbleSkeletonProps } from '@/components/features/game-chat/ChatBubbleSkeleton';

export { ChatHistoryBanner } from '@/components/features/game-chat/ChatHistoryBanner';
export type { ChatHistoryBannerProps } from '@/components/features/game-chat/ChatHistoryBanner';
