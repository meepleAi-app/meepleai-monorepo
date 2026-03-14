'use client';

import React from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useToolboxStore } from '@/lib/stores/toolboxStore';

interface ToolCardDeckProps {
  toolboxId: string;
  deckId: string;
  className?: string;
  'data-testid'?: string;
}

/**
 * Card deck tool content: shows deck state with draw/shuffle/reset actions.
 * Rendered inside ToolCard when type="CardDeck" is expanded.
 * Epic #412 — Game Toolbox.
 */
export function ToolCardDeck({
  toolboxId,
  deckId,
  className = '',
  'data-testid': testId,
}: ToolCardDeckProps) {
  const deckState = useToolboxStore(s => s.deckStates[deckId]);
  const drawCard = useToolboxStore(s => s.drawCard);
  const shuffleDeck = useToolboxStore(s => s.shuffleDeck);
  const resetDeck = useToolboxStore(s => s.resetDeck);

  if (!deckState) {
    return (
      <div
        className="flex items-center justify-center py-4"
        data-testid={testId ?? `tool-card-deck-${deckId}-loading`}
      >
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const { deckName, totalCards, remainingCards, drawnCards } = deckState;
  const canDraw = remainingCards > 0;

  return (
    <div className={`space-y-3 ${className}`} data-testid={testId ?? `tool-card-deck-${deckId}`}>
      {/* Deck info */}
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium">{deckName}</span>
        <span className="text-muted-foreground">
          {remainingCards} / {totalCards} remaining
        </span>
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <Button size="sm" onClick={() => drawCard(toolboxId, deckId)} disabled={!canDraw}>
          Draw
        </Button>
        <Button size="sm" variant="outline" onClick={() => shuffleDeck(toolboxId, deckId)}>
          Shuffle
        </Button>
        <Button size="sm" variant="outline" onClick={() => resetDeck(toolboxId, deckId)}>
          Reset
        </Button>
      </div>

      {/* Drawn cards */}
      {drawnCards.length > 0 && (
        <div className="space-y-1.5">
          <span className="text-xs font-medium text-muted-foreground">
            Drawn ({drawnCards.length})
          </span>
          <div className="flex flex-wrap gap-1.5">
            {drawnCards.map(card => (
              <div
                key={card.id}
                className="flex items-center gap-1 rounded-md border bg-muted/50 px-2 py-1 text-xs"
              >
                <span className="font-medium">{card.name}</span>
                {card.suit && <span className="text-muted-foreground">{card.suit}</span>}
                {card.value && <span className="text-muted-foreground">({card.value})</span>}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
