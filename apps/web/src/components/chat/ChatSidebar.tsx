/**
 * ChatSidebar - Collapsible sidebar with game/agent selection and chat history
 *
 * Composes GameSelector, AgentSelector, ChatHistory, and new chat button.
 * Manages sidebar collapse state and game context badge.
 */

import React from 'react';
import { cn } from '@/lib/utils';
import { useChatContext } from './ChatProvider';
import { GameSelector } from './GameSelector';
import { AgentSelector } from './AgentSelector';
import { ChatHistory } from './ChatHistory';
import { LoadingButton } from '../loading/LoadingButton';

export function ChatSidebar() {
  const {
    games,
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

  return (
    <aside
      aria-label="Chat sidebar with game selection and chat history"
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

        {/* New Chat Button */}
        <LoadingButton
          isLoading={loading.creating}
          loadingText="Creazione..."
          onClick={handleCreateChat}
          disabled={isDisabled}
          aria-label="Create new chat"
          className={cn(
            "w-full py-2.5 text-white border-none rounded text-sm font-medium",
            isDisabled
              ? "bg-[#dadce0] cursor-not-allowed"
              : "bg-[#1a73e8] cursor-pointer"
          )}
        >
          + Nuova Chat
        </LoadingButton>
      </div>

      {/* Chat History */}
      <ChatHistory />
    </aside>
  );
}
