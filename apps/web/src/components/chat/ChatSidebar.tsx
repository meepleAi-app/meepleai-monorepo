/* eslint-disable security/detect-object-injection -- Safe Zustand store key access */
/**
 * ChatSidebar - Sidebar content for chat (Issue #2232)
 *
 * Refactored from Issue #858 to work with ChatLayout pattern.
 * Contains only sidebar content (selectors, new thread button, thread history).
 * Header logic moved to ChatLayout + ChatHeader.
 *
 * Features:
 * - Game/Agent selection
 * - New thread button with limit indicator
 * - Thread history (active + archived)
 */

import React from 'react';

import { cn } from '@/lib/utils';
import { useChatStore } from '@/store/chat/store';
import { ChatThread } from '@/types';

import { AgentSelector } from './AgentSelector';
import { ChatHistory } from './ChatHistory';
import { GameSelector } from './GameSelector';
import { LoadingButton } from '../loading/LoadingButton';

const MAX_THREADS_PER_GAME = 5; // Issue #858: Thread limit constant

export function ChatSidebar() {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const { chatsByGame, selectedGameId, selectedAgentId, loading, createChat } = useChatStore(
    state => ({
      chatsByGame: state.chatsByGame,
      selectedGameId: state.selectedGameId,
      selectedAgentId: state.selectedAgentId,
      loading: state.loading,
      createChat: state.createChat,
    })
  );

  // Derived value
  const chats = selectedGameId ? (chatsByGame[selectedGameId] ?? []) : [];

  const handleCreateChat = () => {
    void createChat();
  };

  const isDisabled = !selectedGameId || !selectedAgentId || loading.creating;

  // Calculate active thread count for current game (Issue #858)
  const activeThreadCount = chats.filter((t: ChatThread) => t.status !== 'Closed').length;
  const isAtThreadLimit = activeThreadCount >= MAX_THREADS_PER_GAME;

  return (
    <div className="flex flex-col h-full">
      {/* Sidebar Header - Selectors and New Thread Button */}
      <div className="p-4 border-b border-[#dadce0] shrink-0">
        {/* Game Selector */}
        <GameSelector />

        {/* Agent Selector */}
        <AgentSelector />

        {/* New Thread Button with limit indicator (Issue #858) */}
        <div>
          <LoadingButton
            isLoading={loading.creating}
            loadingText="Creazione..."
            onClick={handleCreateChat}
            disabled={isDisabled}
            aria-label="Create new thread"
            className={cn(
              'w-full py-2.5 text-white border-none rounded text-sm font-medium',
              isDisabled
                ? 'bg-[#dadce0] cursor-not-allowed'
                : 'bg-[#1a73e8] cursor-pointer hover:bg-[#1557b0]'
            )}
          >
            + Nuovo Thread
          </LoadingButton>

          {/* Thread limit indicator (Issue #858) */}
          {selectedGameId && (
            <div className="mt-2 text-[11px] text-center">
              <span
                className={cn('text-[#5f6368]', isAtThreadLimit && 'text-[#d93025] font-semibold')}
                data-testid="thread-count"
              >
                {activeThreadCount} / {MAX_THREADS_PER_GAME} thread attivi
              </span>
              {isAtThreadLimit && (
                <div className="mt-1 text-[10px] text-[#d93025]" data-testid="thread-limit-warning">
                  (thread più vecchio sarà archiviato)
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Thread History (Issue #858: Now shows active + archived threads) */}
      <div className="flex-1 overflow-hidden">
        <ChatHistory />
      </div>
    </div>
  );
}
