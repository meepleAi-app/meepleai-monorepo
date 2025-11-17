/**
 * MobileSidebar - Mobile-optimized drawer sidebar for chat
 *
 * Renders a Sheet (drawer) component on mobile viewports (< 768px).
 * Contains the same content as ChatSidebar but optimized for touch interaction.
 *
 * Features:
 * - Slides in from left on mobile
 * - Touch-friendly 44x44px minimum targets (WCAG 2.1 AA)
 * - Closes automatically on chat creation
 * - Full-height with proper viewport handling (h-dvh)
 */

import React from 'react';
import { cn } from '@/lib/utils';
import { useChatStore } from '@/store/chat/store';
import { GameSelector } from './GameSelector';
import { AgentSelector } from './AgentSelector';
import { ChatHistory } from './ChatHistory';
import { LoadingButton } from '../loading/LoadingButton';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';

interface MobileSidebarProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function MobileSidebar({ open, onOpenChange }: MobileSidebarProps) {
  const {
    games,
    selectedGameId,
    selectedAgentId,
    loading,
    createChat
  } = useChatStore();

  const handleCreateChat = async () => {
    await createChat();
    // Close drawer after chat creation on mobile
    onOpenChange(false);
  };

  const isDisabled = !selectedGameId || !selectedAgentId || loading.creating;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="left"
        className="w-[320px] p-0 flex flex-col h-dvh"
        aria-label="Mobile chat sidebar"
      >
        <SheetHeader className="p-4 border-b border-[#dadce0] shrink-0">
          <div className="flex justify-between items-center mb-4">
            <SheetTitle className="text-lg">MeepleAI Chat</SheetTitle>
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

          {/* New Chat Button - Touch-friendly 44px min height */}
          <LoadingButton
            isLoading={loading.creating}
            loadingText="Creazione..."
            onClick={handleCreateChat}
            disabled={isDisabled}
            aria-label="Create new chat"
            className={cn(
              "w-full py-2.5 text-white border-none rounded text-sm font-medium touch-target",
              isDisabled
                ? "bg-[#dadce0] cursor-not-allowed"
                : "bg-[#1a73e8] cursor-pointer"
            )}
          >
            + Nuova Chat
          </LoadingButton>
        </SheetHeader>

        {/* Chat History - Scrollable area */}
        <div className="flex-1 overflow-hidden">
          <ChatHistory />
        </div>
      </SheetContent>
    </Sheet>
  );
}
