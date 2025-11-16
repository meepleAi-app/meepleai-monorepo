/**
 * Chat Store Module (Issue #1083)
 *
 * Centralized exports for Zustand-based chat state management.
 *
 * Usage:
 *   import { useChatStore, useActiveChat, useChatStream } from '@/store/chat';
 */

// Main store
export { useChatStore, useTemporalStore } from './store';

// Hooks with auto-generated selectors
export {
  useChatStoreWithSelectors,
  useCurrentChats,
  useActiveChat,
  useActiveMessages,
  useSelectedGame,
  useSelectedAgent,
  useIsLoading,
  useIsCreating,
  useIsSending,
} from './hooks';

// Streaming hook
export { useChatStream } from './useChatStream';
export type { StreamState, StreamControls } from './useChatStream';

// Types
export type {
  ChatStore,
  SessionSlice,
  GameSlice,
  ChatSlice,
  MessagesSlice,
  UISlice,
  LoadingState,
  UndoableAction,
} from './types';
