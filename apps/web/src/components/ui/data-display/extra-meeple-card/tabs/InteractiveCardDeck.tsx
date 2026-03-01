'use client';

/**
 * InteractiveCardDeck - Visual card deck with draw/discard/shuffle operations
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import React from 'react';
import {
  Layers,
  Shuffle,
  Eye,
  ArrowDown,
  ArrowUp,
  Hand,
  Trash2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type { CardDeckState, CardDeckActions, CardEntry } from '../types';

interface InteractiveCardDeckProps {
  state?: CardDeckState;
  actions?: CardDeckActions;
}

/** Visual representation of a card pile (deck/discard) */
function CardPile({
  label,
  count,
  icon: Icon,
  accent,
  topCard,
}: {
  label: string;
  count: number;
  icon: React.ElementType;
  accent: string;
  topCard?: CardEntry;
}) {
  return (
    <div className="flex flex-col items-center gap-1.5">
      <div
        className={cn(
          'relative flex h-20 w-14 items-center justify-center rounded-lg border-2 border-dashed',
          count > 0 ? accent : 'border-slate-200 bg-slate-50'
        )}
        data-testid={`card-pile-${label.toLowerCase()}`}
      >
        {count > 0 ? (
          <>
            {/* Stack effect */}
            {count > 2 && (
              <div className="absolute -top-1 -left-0.5 h-full w-full rounded-lg border border-slate-200 bg-slate-100" />
            )}
            {count > 1 && (
              <div className="absolute -top-0.5 -left-0.5 h-full w-full rounded-lg border border-slate-200 bg-slate-50" />
            )}
            <div className="relative z-10 flex h-full w-full items-center justify-center rounded-lg bg-white border border-slate-200 shadow-sm">
              {topCard?.faceUp ? (
                <span className="font-nunito text-[10px] font-semibold text-slate-600 text-center px-1 line-clamp-2">
                  {topCard.name}
                </span>
              ) : (
                <Icon className="h-5 w-5 text-indigo-400" />
              )}
            </div>
          </>
        ) : (
          <Icon className="h-5 w-5 text-slate-300" />
        )}
      </div>
      <div className="text-center">
        <p className="font-nunito text-[10px] font-semibold text-slate-500 uppercase tracking-wider">
          {label}
        </p>
        <p className="font-nunito text-xs font-bold text-slate-700">{count}</p>
      </div>
    </div>
  );
}

/** Single card in the hand display */
function HandCard({
  card,
  onDiscard,
  onReturnToDeck,
  allowDiscard,
  allowReturn,
}: {
  card: CardEntry;
  onDiscard?: () => void;
  onReturnToDeck?: () => void;
  allowDiscard: boolean;
  allowReturn: boolean;
}) {
  return (
    <div
      className="group flex items-center justify-between rounded-lg bg-white/70 border border-slate-200/60 px-2.5 py-1.5"
      data-testid={`hand-card-${card.id}`}
    >
      <span className="font-nunito text-xs font-medium text-slate-700 truncate">
        {card.name}
      </span>
      <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
        {allowReturn && onReturnToDeck && (
          <button
            onClick={onReturnToDeck}
            className="rounded p-0.5 text-slate-400 hover:text-indigo-500 hover:bg-indigo-50 transition-colors"
            aria-label={`Return ${card.name} to deck`}
          >
            <ArrowUp className="h-3 w-3" />
          </button>
        )}
        {allowDiscard && onDiscard && (
          <button
            onClick={onDiscard}
            className="rounded p-0.5 text-slate-400 hover:text-red-500 hover:bg-red-50 transition-colors"
            aria-label={`Discard ${card.name}`}
          >
            <Trash2 className="h-3 w-3" />
          </button>
        )}
      </div>
    </div>
  );
}

export function InteractiveCardDeck({ state, actions }: InteractiveCardDeckProps) {
  if (!state) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-slate-400">
        <Layers className="h-8 w-8 mb-2 opacity-50" />
        <p className="font-nunito text-sm">No card deck active</p>
      </div>
    );
  }

  const handCards = state.cards.filter((c) => c.zone === 'hand').sort((a, b) => a.order - b.order);
  const topDiscard = state.cards
    .filter((c) => c.zone === 'discard' && c.faceUp)
    .sort((a, b) => b.order - a.order)[0];

  return (
    <div className="space-y-3" data-testid="interactive-card-deck">
      {/* Deck header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <Layers className="h-3.5 w-3.5 text-indigo-500" />
          <span className="font-nunito text-xs font-bold text-slate-700">
            {state.toolName}
          </span>
          <span className="font-nunito text-[10px] text-slate-400">
            {state.deckType}
          </span>
        </div>
      </div>

      {/* Card piles */}
      <div className="flex items-start justify-center gap-6">
        <CardPile
          label="Deck"
          count={state.deckCount}
          icon={Layers}
          accent="border-indigo-300 bg-indigo-50"
        />
        <CardPile
          label="Discard"
          count={state.discardCount}
          icon={Trash2}
          accent="border-amber-300 bg-amber-50"
          topCard={topDiscard}
        />
      </div>

      {/* Action buttons */}
      <div className="flex justify-center gap-2">
        {state.allowDraw && actions?.onDraw && state.deckCount > 0 && (
          <button
            onClick={actions.onDraw}
            className="flex items-center gap-1 rounded-full bg-indigo-50 px-3 py-1.5 font-nunito text-xs font-medium text-indigo-700 hover:bg-indigo-100 transition-colors"
            data-testid="card-action-draw"
          >
            <ArrowDown className="h-3 w-3" />
            Draw
          </button>
        )}
        {state.shuffleable && actions?.onShuffle && state.deckCount > 1 && (
          <button
            onClick={actions.onShuffle}
            className="flex items-center gap-1 rounded-full bg-slate-100 px-3 py-1.5 font-nunito text-xs font-medium text-slate-600 hover:bg-slate-200 transition-colors"
            data-testid="card-action-shuffle"
          >
            <Shuffle className="h-3 w-3" />
            Shuffle
          </button>
        )}
        {state.allowPeek && actions?.onPeek && state.deckCount > 0 && (
          <button
            onClick={actions.onPeek}
            className="flex items-center gap-1 rounded-full bg-slate-100 px-3 py-1.5 font-nunito text-xs font-medium text-slate-600 hover:bg-slate-200 transition-colors"
            data-testid="card-action-peek"
          >
            <Eye className="h-3 w-3" />
            Peek
          </button>
        )}
      </div>

      {/* Hand display */}
      {handCards.length > 0 && (
        <div>
          <div className="mb-1.5 flex items-center gap-1.5">
            <Hand className="h-3 w-3 text-indigo-500" />
            <h4 className="font-nunito text-[10px] font-semibold text-slate-500 uppercase tracking-wider">
              Hand ({handCards.length})
            </h4>
          </div>
          <div className="space-y-1">
            {handCards.map((card) => (
              <HandCard
                key={card.id}
                card={card}
                onDiscard={actions?.onDiscard ? () => actions.onDiscard!(card.id) : undefined}
                onReturnToDeck={actions?.onReturnToDeck ? () => actions.onReturnToDeck!(card.id) : undefined}
                allowDiscard={state.allowDiscard}
                allowReturn={state.allowReturnToDeck}
              />
            ))}
          </div>
        </div>
      )}

      {/* Empty hand message */}
      {handCards.length === 0 && (
        <p className="text-center font-nunito text-xs text-slate-400">
          No cards in hand
        </p>
      )}
    </div>
  );
}
