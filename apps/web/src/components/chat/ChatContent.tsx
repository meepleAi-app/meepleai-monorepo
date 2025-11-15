/**
 * ChatContent - Main chat content area
 *
 * Composes the message list and input form.
 * Displays chat header with game/agent info and sidebar toggle.
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Export chat button
 * - Streaming response display with stop button
 * - Typing indicator
 * - Full integration with useChatStreaming hook
 */

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { useChatContext } from './ChatProvider';
import { MessageList } from './MessageList';
import { MessageInput } from './MessageInput';
import { MobileSidebar } from './MobileSidebar';

export function ChatContent() {
  const {
    games,
    selectedGameId,
    activeChatId,
    chats,
    errorMessage,
    sidebarCollapsed,
    toggleSidebar
  } = useChatContext();

  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [isMobile, setIsMobile] = useState(false);

  const selectedGame = games.find(g => g.id === selectedGameId);
  const activeChat = chats.find(c => c.id === activeChatId);

  // Track mobile viewport with matchMedia
  useEffect(() => {
    const mediaQuery = window.matchMedia('(max-width: 767px)');
    setIsMobile(mediaQuery.matches);

    const handleChange = (e: MediaQueryListEvent) => {
      setIsMobile(e.matches);
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  const handleToggleSidebar = () => {
    // On mobile (< 768px), toggle Sheet instead of desktop sidebar
    if (isMobile) {
      setMobileMenuOpen(!mobileMenuOpen);
    } else {
      toggleSidebar();
    }
  };

  return (
    <div className="flex-1 flex flex-col bg-white">
      {/* Mobile Sidebar */}
      <MobileSidebar open={mobileMenuOpen} onOpenChange={setMobileMenuOpen} />

      {/* Header */}
      <div className="p-4 border-b border-[#dadce0] flex justify-between items-center bg-white">
        <div className="flex items-center gap-3">
          <button
            onClick={handleToggleSidebar}
            aria-label={sidebarCollapsed ? 'Show sidebar' : 'Hide sidebar'}
            aria-expanded={!sidebarCollapsed}
            className="px-3 py-2 bg-[#f1f3f4] border-none rounded cursor-pointer text-lg touch-target"
            title={sidebarCollapsed ? 'Mostra sidebar' : 'Nascondi sidebar'}
          >
            {sidebarCollapsed ? '☰' : '✕'}
          </button>
          <div>
            <h1 className="m-0 text-xl">
              {activeChatId
                ? activeChat?.title ?? 'Chat'
                : 'Seleziona o crea una chat'}
            </h1>
            <p className="mt-1 mb-0 text-[#64748b] text-[13px]">
              {selectedGameId
                ? selectedGame?.name ?? ''
                : 'Nessun gioco selezionato'}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Link
            href="/"
            className="px-4 py-2 bg-[#1a73e8] text-white no-underline rounded text-sm"
          >
            Home
          </Link>
        </div>
      </div>

      {/* Error Message */}
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="m-4 p-3 bg-[#fce8e6] text-[#d93025] rounded text-sm"
        >
          {errorMessage}
        </div>
      )}

      {/* Messages Area */}
      <MessageList />

      {/* Message Input */}
      <MessageInput />
    </div>
  );
}
