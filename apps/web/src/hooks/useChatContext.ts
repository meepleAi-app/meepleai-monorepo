/**
 * Backward compatibility layer for useChatContext
 *
 * This file provides a unified interface from Zustand store:
 * - useAuth() - Authentication state
 * - useChatContext (Zustand) - Game selection, chat ops, UI state, messages
 *
 * This maintains compatibility with components that were using
 * the old monolithic ChatProvider context.
 *
 * Issue #1083: Zustand Chat Store Migration
 */

import React from 'react';
import { useAuth } from '@/components/auth/AuthProvider';
import { useChatContextCompat } from '@/store/chat/ChatStoreProvider';

// Export ChatContextValue from compatibility layer
export type ChatContextValue = ReturnType<typeof useChatContextCompat>;

/**
 * Unified hook that combines all chat-related contexts
 * Maintains backward compatibility with old ChatProvider API
 */
export function useChatContext(): ChatContextValue {
  const auth = useAuth();
  const zustandContext = useChatContextCompat();

  // Memoize to prevent unnecessary re-renders in consuming components
  return React.useMemo<ChatContextValue>(
    () => ({
      // Authentication
      authUser: auth.user,

      // Game & Agent Selection
      games: zustandContext.games,
      selectedGameId: zustandContext.selectedGameId,
      agents: zustandContext.agents,
      selectedAgentId: zustandContext.selectedAgentId,
      selectGame: zustandContext.selectGame,
      selectAgent: zustandContext.selectAgent,

      // Chat Management
      chats: zustandContext.chats,
      activeChatId: zustandContext.activeChatId,
      messages: zustandContext.messages,
      createChat: zustandContext.createChat,
      deleteChat: zustandContext.deleteChat,
      selectChat: zustandContext.selectChat,

      // Messaging
      sendMessage: zustandContext.sendMessage,
      setMessageFeedback: zustandContext.setMessageFeedback,
      editMessage: zustandContext.editMessage,
      deleteMessage: zustandContext.deleteMessage,

      // UI State
      loading: zustandContext.loading,
      errorMessage: zustandContext.errorMessage ?? auth.error ?? '',
      sidebarCollapsed: zustandContext.sidebarCollapsed,
      toggleSidebar: zustandContext.toggleSidebar,

      // Message Edit State
      editingMessageId: zustandContext.editingMessageId,
      editContent: zustandContext.editContent,
      setEditContent: zustandContext.setEditContent,
      startEditMessage: zustandContext.startEditMessage,
      cancelEdit: zustandContext.cancelEdit,
      saveEdit: zustandContext.saveEdit,

      // Input State
      inputValue: zustandContext.inputValue,
      setInputValue: zustandContext.setInputValue,

      // Search Mode State
      searchMode: zustandContext.searchMode,
      setSearchMode: zustandContext.setSearchMode,
    }),
    [auth.user, auth.error, zustandContext]
  );
}
