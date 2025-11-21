/**
 * Chat Store Module (Issue #1083)
 *
 * Centralized exports for Zustand-based chat state management.
 *
 * Usage:
 *   import { useChatStore, useActiveChat } from '@/store/chat';
 *
 * Note: For streaming functionality, use useChatStreaming from '@/lib/hooks/useChatStreaming'
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
