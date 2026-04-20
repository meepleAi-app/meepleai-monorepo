/**
 * Chat Entry Components — barrel export
 *
 * Decomposed from the 847-line NewChatView into focused, reusable components.
 * ChatSlideOverPanel can import individual components for its own composition.
 */

export { GameSelector } from './GameSelector';
export type { GameSelectorProps } from './GameSelector';

export { AgentSelector } from './AgentSelector';
export type { AgentSelectorProps } from './AgentSelector';

export { QuickStartSuggestions } from './QuickStartSuggestions';
export type { QuickStartSuggestionsProps } from './QuickStartSuggestions';

export { createThread, createThreadWithContext, resolveAgentId } from './ThreadCreator';
export type { CreateThreadParams, CreateThreadResult } from './ThreadCreator';

export { ChatEntryOrchestrator } from './ChatEntryOrchestrator';
export type { ChatEntryOrchestratorProps } from './ChatEntryOrchestrator';

export { DEFAULT_AGENTS, getQuickStartSuggestions } from './constants';
export type { AgentOption, CustomAgent, QuickStartSuggestion, PromptType } from './types';
