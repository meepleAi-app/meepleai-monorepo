/**
 * BottomNav - Mobile bottom navigation for chat actions
 *
 * Displays key chat actions at the bottom of the screen on mobile viewports.
 * Fixed position navigation bar with touch-friendly targets (44x44px minimum).
 *
 * Features:
 * - Fixed bottom positioning on mobile (< 768px)
 * - Hidden on desktop (≥ 768px)
 * - Touch-friendly action buttons (WCAG 2.1 AA compliant)
 * - Consistent with MeepleAI design system
 */

import React from 'react';
import { cn } from '@/lib/utils';
import { useChatContext } from '@/hooks/useChatContext';
import { LoadingButton } from '../loading/LoadingButton';

export function BottomNav() {
  const { selectedGameId, selectedAgentId, loading, createChat } = useChatContext();

  const handleCreateChat = () => {
    void createChat();
  };

  const isDisabled = !selectedGameId || !selectedAgentId || loading.creating;

  return (
    <nav
      className="md:hidden fixed bottom-0 left-0 right-0 bg-white border-t border-[#dadce0] p-4 z-50"
      aria-label="Mobile chat actions"
    >
      <div className="flex gap-3 max-w-md mx-auto">
        {/* New Chat Button - Touch-friendly with 44px min height */}
        <LoadingButton
          isLoading={loading.creating}
          loadingText="Creazione..."
          onClick={handleCreateChat}
          disabled={isDisabled}
          aria-label="Create new chat on mobile"
          className={cn(
            'flex-1 py-2.5 text-white border-none rounded text-sm font-medium touch-target',
            isDisabled
              ? 'bg-[#dadce0] cursor-not-allowed'
              : 'bg-[#1a73e8] cursor-pointer hover:bg-[#1557b0]'
          )}
        >
          + Nuova Chat
        </LoadingButton>
      </div>
    </nav>
  );
}
