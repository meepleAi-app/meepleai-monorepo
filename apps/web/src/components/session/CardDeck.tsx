'use client';

/**
 * CardDeck Component (Issue #3343)
 *
 * Interactive card deck management for game sessions.
 * Features:
 * - Visual deck representation
 * - Draw, discard, and shuffle operations
 * - Player hand management
 * - Shared discard pile visibility
 * - Card flip animations
 */

import { useState, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { Card, SessionDeck } from './types';

// ============================================================================
// Types
// ============================================================================

export interface CardDeckProps {
  /** Deck information */
  deck: SessionDeck;

  /** Player's hand */
  hand: Card[];

  /** Discard pile (top cards) */
  discardPile: Card[];

  /** Whether actions are loading */
  isLoading?: boolean;

  /** Draw cards callback */
  onDraw?: (count: number) => Promise<void>;

  /** Discard cards callback */
  onDiscard?: (cardIds: string[]) => Promise<void>;

  /** Shuffle deck callback */
  onShuffle?: (includeDiscard?: boolean) => Promise<void>;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function getCardSuitInfo(suit?: string): { symbol: string; color: string } {
  const suits: Record<string, { symbol: string; color: string }> = {
    Hearts: { symbol: '♥', color: 'text-red-500' },
    Diamonds: { symbol: '♦', color: 'text-red-500' },
    Clubs: { symbol: '♣', color: 'text-gray-900 dark:text-white' },
    Spades: { symbol: '♠', color: 'text-gray-900 dark:text-white' },
    Joker: { symbol: '🃏', color: 'text-purple-500' },
  };
  return suits[suit || ''] || { symbol: '', color: 'text-gray-500' };
}

function getCardDisplayValue(value?: string): string {
  if (!value) return '';
  const valueMap: Record<string, string> = {
    Ace: 'A',
    Jack: 'J',
    Queen: 'Q',
    King: 'K',
  };
  // eslint-disable-next-line security/detect-object-injection
  return valueMap[value] || value;
}

// ============================================================================
// Sub-Components
// ============================================================================

interface PlayingCardProps {
  card: Card;
  isSelected?: boolean;
  isFaceDown?: boolean;
  onClick?: () => void;
  className?: string;
}

function PlayingCard({ card, isSelected, isFaceDown, onClick, className }: PlayingCardProps) {
  const suitInfo = getCardSuitInfo(card.suit);
  const displayValue = getCardDisplayValue(card.value);

  return (
    <motion.div
      layout
      initial={{ scale: 0.8, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      exit={{ scale: 0.8, opacity: 0 }}
      whileHover={{ y: -8, transition: { duration: 0.2 } }}
      whileTap={{ scale: 0.95 }}
      onClick={onClick}
      className={cn(
        'relative w-16 h-24 sm:w-20 sm:h-28 rounded-lg cursor-pointer',
        'shadow-md hover:shadow-lg transition-shadow',
        'select-none',
        isSelected && 'ring-2 ring-primary ring-offset-2',
        className
      )}
    >
      {isFaceDown ? (
        // Card back
        <div className="w-full h-full bg-gradient-to-br from-blue-600 to-blue-800 rounded-lg border-2 border-white/20">
          <div className="w-full h-full flex items-center justify-center">
            <div className="w-10 h-14 sm:w-12 sm:h-16 rounded border border-white/30 bg-blue-700/50">
              <div className="w-full h-full flex items-center justify-center text-white/50 text-xs">
                🎴
              </div>
            </div>
          </div>
        </div>
      ) : (
        // Card face
        <div className="w-full h-full bg-white dark:bg-gray-100 rounded-lg border border-gray-200 p-1">
          {card.imageUrl ? (
            // Custom card with image
            <div className="w-full h-full flex flex-col items-center justify-center">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={card.imageUrl}
                alt={card.name}
                className="w-full h-full object-cover rounded"
              />
            </div>
          ) : (
            // Standard playing card
            <div className="w-full h-full flex flex-col">
              {/* Top-left corner */}
              <div className={cn('text-xs sm:text-sm font-bold leading-none', suitInfo.color)}>
                <div>{displayValue}</div>
                <div className="text-base">{suitInfo.symbol}</div>
              </div>

              {/* Center */}
              <div className={cn('flex-1 flex items-center justify-center text-2xl sm:text-3xl', suitInfo.color)}>
                {suitInfo.symbol}
              </div>

              {/* Bottom-right corner (rotated) */}
              <div className={cn('text-xs sm:text-sm font-bold leading-none self-end rotate-180', suitInfo.color)}>
                <div>{displayValue}</div>
                <div className="text-base">{suitInfo.symbol}</div>
              </div>
            </div>
          )}
        </div>
      )}
    </motion.div>
  );
}

interface DeckStackProps {
  cardCount: number;
  onClick?: () => void;
  label?: string;
}

function DeckStack({ cardCount, onClick, label }: DeckStackProps) {
  // Show up to 5 stacked cards visually
  const visibleCards = Math.min(cardCount, 5);

  return (
    <div className="relative">
      <div
        className="relative w-16 h-24 sm:w-20 sm:h-28 cursor-pointer"
        onClick={onClick}
      >
        {Array.from({ length: visibleCards }).map((_, index) => (
          <div
            key={index}
            className="absolute w-full h-full bg-gradient-to-br from-blue-600 to-blue-800 rounded-lg border-2 border-white/20 shadow-sm"
            style={{
              top: -index * 2,
              left: index * 1,
              zIndex: visibleCards - index,
            }}
          />
        ))}
        {cardCount === 0 && (
          <div className="w-full h-full border-2 border-dashed border-gray-300 dark:border-gray-600 rounded-lg flex items-center justify-center">
            <span className="text-gray-400 text-xs">Empty</span>
          </div>
        )}
      </div>
      {label && (
        <div className="text-center mt-2 text-sm font-medium text-muted-foreground">
          {label}
          <span className="ml-1 text-xs">({cardCount})</span>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function CardDeck({
  deck,
  hand,
  discardPile,
  isLoading = false,
  onDraw,
  onDiscard,
  onShuffle,
  className,
}: CardDeckProps) {
  const [selectedCards, setSelectedCards] = useState<Set<string>>(new Set());
  const [drawCount, setDrawCount] = useState(1);
  const [isDrawing, setIsDrawing] = useState(false);
  const [isDiscarding, setIsDiscarding] = useState(false);
  const [isShuffling, setIsShuffling] = useState(false);

  const toggleCardSelection = useCallback((cardId: string) => {
    setSelectedCards(prev => {
      const next = new Set(prev);
      if (next.has(cardId)) {
        next.delete(cardId);
      } else {
        next.add(cardId);
      }
      return next;
    });
  }, []);

  const handleDraw = useCallback(async () => {
    if (!onDraw || isDrawing) return;
    setIsDrawing(true);
    try {
      await onDraw(drawCount);
    } finally {
      setIsDrawing(false);
    }
  }, [onDraw, drawCount, isDrawing]);

  const handleDiscard = useCallback(async () => {
    if (!onDiscard || selectedCards.size === 0 || isDiscarding) return;
    setIsDiscarding(true);
    try {
      await onDiscard(Array.from(selectedCards));
      setSelectedCards(new Set());
    } finally {
      setIsDiscarding(false);
    }
  }, [onDiscard, selectedCards, isDiscarding]);

  const handleShuffle = useCallback(async (includeDiscard = false) => {
    if (!onShuffle || isShuffling) return;
    setIsShuffling(true);
    try {
      await onShuffle(includeDiscard);
    } finally {
      setIsShuffling(false);
    }
  }, [onShuffle, isShuffling]);

  return (
    <div className={cn('space-y-6', className)}>
      {/* Deck Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">{deck.name}</h3>
          <p className="text-sm text-muted-foreground">
            {deck.totalCards} total cards • {deck.deckType}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleShuffle(false)}
            disabled={isLoading || isShuffling || deck.cardsInDrawPile === 0}
          >
            {isShuffling ? 'Shuffling...' : '🔀 Shuffle'}
          </Button>
          {deck.cardsInDiscardPile > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => handleShuffle(true)}
              disabled={isLoading || isShuffling}
            >
              Shuffle All
            </Button>
          )}
        </div>
      </div>

      {/* Draw Pile and Discard Pile */}
      <div className="flex items-start justify-center gap-8 sm:gap-12">
        {/* Draw Pile */}
        <div className="flex flex-col items-center gap-3">
          <DeckStack
            cardCount={deck.cardsInDrawPile}
            onClick={handleDraw}
            label="Draw Pile"
          />
          <div className="flex items-center gap-2">
            <select
              value={drawCount}
              onChange={(e) => setDrawCount(Number(e.target.value))}
              className="w-16 h-8 text-sm border rounded px-2"
              disabled={isLoading}
            >
              {[1, 2, 3, 4, 5, 7, 10].map(n => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
            <Button
              size="sm"
              onClick={handleDraw}
              disabled={isLoading || isDrawing || deck.cardsInDrawPile === 0}
            >
              {isDrawing ? 'Drawing...' : 'Draw'}
            </Button>
          </div>
        </div>

        {/* Discard Pile */}
        <div className="flex flex-col items-center">
          {discardPile.length > 0 ? (
            <div className="relative">
              <PlayingCard
                card={discardPile[0]}
                className="opacity-100"
              />
              {discardPile.length > 1 && (
                <div className="absolute -bottom-1 -right-1 w-6 h-6 rounded-full bg-primary text-primary-foreground text-xs flex items-center justify-center font-medium">
                  +{discardPile.length - 1}
                </div>
              )}
            </div>
          ) : (
            <DeckStack cardCount={0} label="Discard" />
          )}
          <div className="text-center mt-2 text-sm font-medium text-muted-foreground">
            Discard Pile
            <span className="ml-1 text-xs">({deck.cardsInDiscardPile})</span>
          </div>
        </div>
      </div>

      {/* Player's Hand */}
      {hand.length > 0 && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="font-medium">Your Hand ({hand.length})</h4>
            {selectedCards.size > 0 && (
              <Button
                variant="destructive"
                size="sm"
                onClick={handleDiscard}
                disabled={isLoading || isDiscarding}
              >
                {isDiscarding ? 'Discarding...' : `Discard ${selectedCards.size}`}
              </Button>
            )}
          </div>
          <div className="flex flex-wrap gap-2 sm:gap-3 p-4 bg-muted/50 rounded-lg min-h-[120px]">
            <AnimatePresence mode="popLayout">
              {hand.map((card) => (
                <PlayingCard
                  key={card.id}
                  card={card}
                  isSelected={selectedCards.has(card.id)}
                  onClick={() => toggleCardSelection(card.id)}
                />
              ))}
            </AnimatePresence>
          </div>
          <p className="text-xs text-muted-foreground">
            Click cards to select, then discard selected cards
          </p>
        </div>
      )}

      {/* Empty Hand Message */}
      {hand.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          <p>Your hand is empty</p>
          <p className="text-sm">Draw cards from the deck to start</p>
        </div>
      )}
    </div>
  );
}

export default CardDeck;
