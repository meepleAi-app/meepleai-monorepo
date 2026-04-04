'use client';

/**
 * DeckStackDrawer - Side drawer showing card browsing history
 *
 * Features:
 * - Displays history stack in reverse chronological order
 * - Highlights current card with entity-color ring
 * - Tap any history entry to navigate to it
 * - Clear history button at footer
 *
 * @module components/ui/data-display/meeple-card-browser/DeckStackDrawer
 */

import React from 'react';

import { Trash2 } from 'lucide-react';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/navigation/sheet';
import { cn } from '@/lib/utils';

import { useCardBrowser, type CardRef } from './CardBrowserContext';
import { MeepleCard } from '../meeple-card';

interface DeckStackDrawerProps {
  isOpen: boolean;
  currentCardId: string;
  onClose: () => void;
}

export function DeckStackDrawer({ isOpen, currentCardId, onClose }: DeckStackDrawerProps) {
  const { history, navigateTo, clearHistory } = useCardBrowser();

  const handleCardTap = (card: CardRef) => {
    navigateTo(card);
    onClose();
  };

  return (
    <Sheet
      open={isOpen}
      onOpenChange={open => {
        if (!open) onClose();
      }}
    >
      <SheetContent side="right" className="w-[80vw] max-w-[320px] flex flex-col">
        <SheetHeader>
          <SheetTitle data-testid="deck-stack-title">History ({history.length})</SheetTitle>
          <SheetDescription className="sr-only">Card browsing history</SheetDescription>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto space-y-2 py-2" data-testid="deck-stack-list">
          {[...history].reverse().map(card => (
            <button
              key={card.id}
              onClick={() => handleCardTap(card)}
              className={cn(
                'w-full text-left rounded-lg transition-colors',
                card.id === currentCardId ? 'ring-2 bg-muted/50' : 'hover:bg-muted/30'
              )}
              style={
                card.id === currentCardId
                  ? ({ '--tw-ring-color': `hsl(${card.color})` } as React.CSSProperties)
                  : undefined
              }
              data-testid="deck-stack-entry"
            >
              <MeepleCard
                entity={card.entity}
                variant="compact"
                title={card.title}
                subtitle={card.subtitle}
                imageUrl={card.imageUrl}
              />
            </button>
          ))}
          {history.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">No history yet</p>
          )}
        </div>

        {history.length > 0 && (
          <div className="border-t pt-2 pb-2">
            <button
              onClick={() => {
                clearHistory();
                onClose();
              }}
              className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground w-full justify-center min-h-[44px]"
              data-testid="clear-history-button"
            >
              <Trash2 size={16} />
              Clear history
            </button>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}
