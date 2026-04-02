'use client';

import { useEffect } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

interface CardDeckToolProps {
  deckId: string;
  name: string;
  cards: string[];
  reshuffleOnEmpty: boolean;
  onAction?: (action: string, result: string) => void;
}

export function CardDeckTool({
  deckId,
  name,
  cards,
  reshuffleOnEmpty,
  onAction,
}: CardDeckToolProps) {
  const { decks, initDeck, drawCard, shuffleDeck, resetDeck, undoDraw } =
    useStandaloneToolkitStore();

  const deck = decks[deckId];

  useEffect(() => {
    if (!deck) {
      initDeck(deckId, name, cards, reshuffleOnEmpty);
    }
  }, [deckId]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!deck) return null;

  const canUndo = Boolean(deck.undoExpiry && Date.now() < deck.undoExpiry);
  const isEmpty =
    deck.drawPile.length === 0 && (!reshuffleOnEmpty || deck.discardPile.length === 0);

  const handleDraw = () => {
    const card = drawCard(deckId);
    if (card) {
      // Add drawn card to discard pile while preserving the undo snapshot set by drawCard
      useStandaloneToolkitStore.setState(s => {
        const current = s.decks[deckId];
        if (!current) return s;
        return {
          decks: {
            ...s.decks,
            [deckId]: {
              ...current,
              discardPile: [...current.discardPile, card],
            },
          },
        };
      });
      onAction?.('draw', card);
    }
  };

  const handleUndo = () => {
    const ok = undoDraw(deckId);
    if (ok) onAction?.('undo', 'Draw annullato');
  };

  const handleShuffle = () => {
    shuffleDeck(deckId);
    onAction?.('shuffle', 'Mazzo rimescolato');
  };

  const handleReset = () => {
    resetDeck(deckId);
    onAction?.('reset', 'Mazzo resettato');
  };

  return (
    <div className="space-y-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between">
        <span className="text-sm font-semibold text-slate-700">{deck.name}</span>
        <span className="text-xs text-slate-500">{deck.drawPile.length} carte</span>
      </div>

      {deck.lastDrawnCard && (
        <div
          className="rounded-md border-2 border-blue-300 bg-blue-50 p-3 text-center text-sm font-medium text-blue-800"
          data-testid="drawn-card"
        >
          {deck.lastDrawnCard}
        </div>
      )}

      {isEmpty && <p className="text-center text-xs text-slate-400">Mazzo esaurito</p>}

      <div className="flex gap-2">
        <Button
          onClick={handleDraw}
          disabled={isEmpty}
          className="flex-1"
          aria-label="Pesca una carta"
        >
          Pesca
        </Button>
        {canUndo && (
          <Button variant="outline" onClick={handleUndo} aria-label="Annulla ultimo draw">
            Annulla
          </Button>
        )}
      </div>

      <div className="flex gap-2">
        <Button variant="outline" size="sm" onClick={handleShuffle} className="flex-1">
          🔀 Rimescola
        </Button>
        <Button variant="outline" size="sm" onClick={handleReset} className="flex-1">
          ↩ Reset
        </Button>
      </div>

      <p className="text-center text-xs text-slate-400">Scarti: {deck.discardPile.length}</p>
    </div>
  );
}
