'use client';

import React from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import type { DrawnCardDto } from '@/lib/api/schemas/toolbox.schemas';
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
  const deckState = useToolboxStore(s => s.cardDecks[deckId]);
  const draw = useToolboxStore(s => s.draw);
  const shuffle = useToolboxStore(s => s.shuffle);
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

  const { name, totalCards, remainingInDeck, drawnCards } = deckState;
  const canDraw = remainingInDeck > 0;

  return (
    <div className={`space-y-3 ${className}`} data-testid={testId ?? `tool-card-deck-${deckId}`}>
      {/* Deck info */}
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium">{name}</span>
        <span className="text-muted-foreground">
          {remainingInDeck} / {totalCards} remaining
        </span>
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <Button size="sm" onClick={() => draw(deckId, 1)} disabled={!canDraw}>
          Draw
        </Button>
        <Button size="sm" variant="outline" onClick={() => shuffle(deckId)}>
          Shuffle
        </Button>
        <Button size="sm" variant="outline" onClick={() => resetDeck(deckId)}>
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
            {drawnCards.map((card: DrawnCardDto) => (
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
