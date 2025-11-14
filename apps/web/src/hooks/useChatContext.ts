/**
 * Backward compatibility layer for useChatContext
 *
 * This file provides a unified interface that combines:
 * - useAuth() - Authentication state
 * - useGame() - Game and agent selection
 * - useChat() - Chat and message operations
 * - useUI() - UI state (sidebar, editing, input)
 *
 * This maintains compatibility with components that were using
 * the old monolithic ChatProvider context.
 */

import React from 'react';
import { useAuth } from '@/components/auth/AuthProvider';
import { useGame } from '@/components/game/GameProvider';
import { useChat } from '@/components/chat/ChatProvider';
import { useUI } from '@/components/ui/UIProvider';

export interface ChatContextValue {
  // Authentication (from AuthProvider)
  authUser: ReturnType<typeof useAuth>['user'];

  // Game & Agent Selection (from GameProvider)
  games: ReturnType<typeof useGame>['games'];
  selectedGameId: ReturnType<typeof useGame>['selectedGameId'];
  agents: ReturnType<typeof useGame>['agents'];
  selectedAgentId: ReturnType<typeof useGame>['selectedAgentId'];
  selectGame: ReturnType<typeof useGame>['selectGame'];
  selectAgent: ReturnType<typeof useGame>['selectAgent'];

  // Chat Management (from ChatProvider)
  chats: ReturnType<typeof useChat>['chats'];
  activeChatId: ReturnType<typeof useChat>['activeChatId'];
  messages: ReturnType<typeof useChat>['messages'];
  createChat: ReturnType<typeof useChat>['createChat'];
  deleteChat: ReturnType<typeof useChat>['deleteChat'];
  selectChat: ReturnType<typeof useChat>['selectChat'];

  // Messaging (from ChatProvider)
  sendMessage: ReturnType<typeof useChat>['sendMessage'];
  setMessageFeedback: ReturnType<typeof useChat>['setMessageFeedback'];
  editMessage: ReturnType<typeof useChat>['editMessage'];
  deleteMessage: ReturnType<typeof useChat>['deleteMessage'];

  // UI State (from UIProvider)
  loading: {
    games: ReturnType<typeof useGame>['loading']['games'];
    agents: ReturnType<typeof useGame>['loading']['agents'];
    chats: ReturnType<typeof useChat>['loading']['chats'];
    messages: ReturnType<typeof useChat>['loading']['messages'];
    sending: ReturnType<typeof useChat>['loading']['sending'];
    creating: ReturnType<typeof useChat>['loading']['creating'];
    updating: ReturnType<typeof useChat>['loading']['updating'];
    deleting: ReturnType<typeof useChat>['loading']['deleting'];
  };
  errorMessage: string;
  sidebarCollapsed: ReturnType<typeof useUI>['sidebarCollapsed'];
  toggleSidebar: ReturnType<typeof useUI>['toggleSidebar'];

  // Message Edit State (from UIProvider)
  editingMessageId: ReturnType<typeof useUI>['editingMessageId'];
  editContent: ReturnType<typeof useUI>['editContent'];
  setEditContent: ReturnType<typeof useUI>['setEditContent'];
  startEditMessage: ReturnType<typeof useUI>['startEdit'];
  cancelEdit: ReturnType<typeof useUI>['cancelEdit'];
  saveEdit: () => Promise<void>;

  // Input State (from UIProvider)
  inputValue: ReturnType<typeof useUI>['inputValue'];
  setInputValue: ReturnType<typeof useUI>['setInputValue'];

  // Search Mode State (from UIProvider)
  searchMode: ReturnType<typeof useUI>['searchMode'];
  setSearchMode: ReturnType<typeof useUI>['setSearchMode'];
}

/**
 * Unified hook that combines all chat-related contexts
 * Maintains backward compatibility with old ChatProvider API
 */
export function useChatContext(): ChatContextValue {
  const auth = useAuth();
  const game = useGame();
  const chat = useChat();
  const ui = useUI();

  // Memoize to prevent unnecessary re-renders in consuming components
  return React.useMemo<ChatContextValue>(
    () => ({
      // Authentication
      authUser: auth.user,

      // Game & Agent Selection
      games: game.games,
      selectedGameId: game.selectedGameId,
      agents: game.agents,
      selectedAgentId: game.selectedAgentId,
      selectGame: game.selectGame,
      selectAgent: game.selectAgent,

      // Chat Management
      chats: chat.chats,
      activeChatId: chat.activeChatId,
      messages: chat.messages,
      createChat: chat.createChat,
      deleteChat: chat.deleteChat,
      selectChat: chat.selectChat,

      // Messaging
      sendMessage: chat.sendMessage,
      setMessageFeedback: chat.setMessageFeedback,
      editMessage: chat.editMessage,
      deleteMessage: chat.deleteMessage,

      // UI State
      loading: {
        games: game.loading.games,
        agents: game.loading.agents,
        chats: chat.loading.chats,
        messages: chat.loading.messages,
        sending: chat.loading.sending,
        creating: chat.loading.creating,
        updating: chat.loading.updating,
        deleting: chat.loading.deleting,
      },
      errorMessage: chat.error ?? game.error ?? auth.error ?? '',
      sidebarCollapsed: ui.sidebarCollapsed,
      toggleSidebar: ui.toggleSidebar,

      // Message Edit State
      editingMessageId: ui.editingMessageId,
      editContent: ui.editContent,
      setEditContent: ui.setEditContent,
      startEditMessage: ui.startEdit,
      cancelEdit: ui.cancelEdit,
      saveEdit: async () => {
        await ui.saveEdit(chat.editMessage);
      },

      // Input State
      inputValue: ui.inputValue,
      setInputValue: ui.setInputValue,

      // Search Mode State
      searchMode: ui.searchMode,
      setSearchMode: ui.setSearchMode,
    }),
    [
      auth.user,
      auth.error,
      game.games,
      game.selectedGameId,
      game.agents,
      game.selectedAgentId,
      game.selectGame,
      game.selectAgent,
      game.loading,
      game.error,
      chat.chats,
      chat.activeChatId,
      chat.messages,
      chat.createChat,
      chat.deleteChat,
      chat.selectChat,
      chat.sendMessage,
      chat.setMessageFeedback,
      chat.editMessage,
      chat.deleteMessage,
      chat.loading,
      chat.error,
      ui.sidebarCollapsed,
      ui.toggleSidebar,
      ui.editingMessageId,
      ui.editContent,
      ui.setEditContent,
      ui.startEdit,
      ui.cancelEdit,
      ui.saveEdit,
      ui.inputValue,
      ui.setInputValue,
      ui.searchMode,
      ui.setSearchMode,
    ]
  );
}
