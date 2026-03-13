/**
 * CardFocusLayout - Mobile layout orchestrator for card-focus view
 *
 * Arranges HandStack (left edge) + FocusedCardArea (center) for mobile.
 * Reads from useHandContext store for state management.
 *
 * Usage: In AppShell, render this layout when isMobile is true.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { type ReactNode } from 'react';

import { useHandContext, type HandContextType } from '@/hooks/use-hand-context';
import { cn } from '@/lib/utils';

import { FocusedCardArea } from './focused-card-area';
import { HandStack } from './hand-stack';

// ============================================================================
// Context labels
// ============================================================================

const CONTEXT_LABELS: Record<HandContextType, string> = {
  library: 'La tua mano',
  sessions: 'Le tue sessioni',
  agents: 'I tuoi agenti',
  chats: 'Le tue chat',
  kbs: 'Le tue KB',
  toolkits: 'I tuoi toolkit',
  players: 'I tuoi giocatori',
};

// ============================================================================
// Component
// ============================================================================

interface CardFocusLayoutProps {
  children: ReactNode;
  className?: string;
}

export function CardFocusLayout({ children, className }: CardFocusLayoutProps) {
  const { cards, focusedIdx, handContext, focusCard, swipeNext, swipePrev } = useHandContext();

  const contextLabel = CONTEXT_LABELS[handContext] ?? 'La tua mano';
  const hasPrev = focusedIdx > 0;
  const hasNext = focusedIdx < cards.length - 1;

  return (
    <div
      className={cn('flex h-full', className)}
      data-testid="card-focus-layout"
    >
      {/* Left: Hand stack */}
      <div className="flex flex-col border-r border-border/30 bg-background/50">
        <div className="px-1.5 py-2">
          <p className="text-[8px] font-bold uppercase tracking-wider text-muted-foreground/60 text-center font-nunito">
            {contextLabel}
          </p>
        </div>
        <HandStack
          cards={cards}
          focusedIdx={focusedIdx}
          onCardClick={focusCard}
        />
      </div>

      {/* Center: Focused card area */}
      <FocusedCardArea
        onSwipeLeft={swipeNext}
        onSwipeRight={swipePrev}
        hasPrev={hasPrev}
        hasNext={hasNext}
      >
        {children}
      </FocusedCardArea>
    </div>
  );
}
