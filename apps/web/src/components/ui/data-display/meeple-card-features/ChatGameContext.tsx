/**
 * ChatGameContext - Game Context Chip
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Displays the game associated with a chat session as a compact chip
 * with an orange dot (game entity color) and optional link.
 */

'use client';

import React from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatGame {
  /** Game display name */
  name: string;
  /** Optional game ID for linking */
  id?: string;
}

export interface ChatGameContextProps {
  /** Game context info */
  game: ChatGame;
  /** Custom className */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const ChatGameContext = React.memo(function ChatGameContext({
  game,
  className,
}: ChatGameContextProps) {
  const content = (
    <>
      {/* Orange dot - game entity color */}
      <span
        className="w-2 h-2 rounded-full flex-shrink-0"
        style={{ backgroundColor: 'hsl(25 95% 45%)' }}
        aria-hidden="true"
      />
      <span className="truncate">{game.name}</span>
    </>
  );

  const chipClasses = cn(
    'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md',
    'text-[11px] font-medium text-muted-foreground',
    'bg-muted/60 border border-border/30',
    'max-w-[160px]',
    game.id && 'hover:bg-muted hover:text-foreground transition-colors cursor-pointer',
    className
  );

  if (game.id) {
    return (
      <a
        href={`/games/${game.id}`}
        className={chipClasses}
        data-testid="chat-game-context"
        onClick={(e) => e.stopPropagation()}
      >
        {content}
      </a>
    );
  }

  return (
    <span className={chipClasses} data-testid="chat-game-context">
      {content}
    </span>
  );
});

ChatGameContext.displayName = 'ChatGameContext';
