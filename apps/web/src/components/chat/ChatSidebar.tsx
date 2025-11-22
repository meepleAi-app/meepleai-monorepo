/**
 * ChatSidebar - Collapsible sidebar with game/agent selection and thread history (Issue #858)
 *
 * Composes GameSelector, AgentSelector, ChatHistory (thread list), and new thread button.
 * Manages sidebar collapse state and game context badge.
 *
 * Updated for SPRINT-3 #858:
 * - Thread-based UI (replacing chat sessions)
 * - Shows active and archived threads
 * - Thread limit indicator (max 5 per game)
 * - Hybrid creation: manual button + auto-create on first message
 */

import React from 'react';
import { cn } from '@/lib/utils';
import { useChatContext } from './ChatProvider';
import { GameSelector } from './GameSelector';
import { AgentSelector } from './AgentSelector';
import { ChatHistory } from './ChatHistory';
import { LoadingButton } from '../loading/LoadingButton';

const MAX_THREADS_PER_GAME = 5; // Issue #858: Thread limit constant

export function ChatSidebar() {
  const {
    games,
    chats,
    selectedGameId,
    selectedAgentId,
    sidebarCollapsed,
    loading,
    createChat
  } = useChatContext();

  const handleCreateChat = () => {
    void createChat();
  };

  const isDisabled = !selectedGameId || !selectedAgentId || loading.creating;

  // Calculate active thread count for current game (Issue #858)
  const activeThreadCount = chats.filter(t => t.status !== 'Closed').length;
  const isAtThreadLimit = activeThreadCount >= MAX_THREADS_PER_GAME;

  return (
    <aside
      aria-label="Chat sidebar with game selection and thread history"
      className={cn(
        "hidden md:flex bg-[#f8f9fa] border-r border-[#dadce0] flex-col overflow-hidden transition-[width,min-width] duration-300 ease-in-out",
        sidebarCollapsed ? "w-0 min-w-0" : "w-80 min-w-[320px]"
      )}
    >
      {/* Sidebar Header */}
      <div className="p-4 border-b border-[#dadce0]">
        <div className="flex justify-between items-center mb-4">
          <h2 className="m-0 text-lg">MeepleAI Chat</h2>
          {/* Game context badge */}
          {selectedGameId && (
            <div
              className="px-3 py-1 bg-[#e8f0fe] text-[#1a73e8] rounded-xl text-[11px] font-semibold border border-[#1a73e8]"
              title={`Currently chatting about: ${games.find(g => g.id === selectedGameId)?.name ?? 'Unknown game'}`}
              aria-label={`Active game context: ${games.find(g => g.id === selectedGameId)?.name ?? 'Unknown game'}`}
            >
              {games.find(g => g.id === selectedGameId)?.name ?? '...'}
            </div>
          )}
        </div>

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
              "w-full py-2.5 text-white border-none rounded text-sm font-medium",
              isDisabled
                ? "bg-[#dadce0] cursor-not-allowed"
                : "bg-[#1a73e8] cursor-pointer hover:bg-[#1557b0]"
            )}
          >
            + Nuovo Thread
          </LoadingButton>

          {/* Thread limit indicator (Issue #858) */}
          {selectedGameId && (
            <div className="mt-2 text-[11px] text-center">
              <span className={cn(
                "text-[#5f6368]",
                isAtThreadLimit && "text-[#d93025] font-semibold"
              )}>
                {activeThreadCount} / {MAX_THREADS_PER_GAME} thread attivi
              </span>
              {isAtThreadLimit && (
                <div className="mt-1 text-[10px] text-[#d93025]">
                  (thread più vecchio sarà archiviato)
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Thread History (Issue #858: Now shows active + archived threads) */}
      <ChatHistory />
    </aside>
  );
}
