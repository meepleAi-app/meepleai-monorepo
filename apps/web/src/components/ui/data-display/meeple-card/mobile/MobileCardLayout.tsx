'use client';

import { useState, useCallback } from 'react';

import { FocusedCard } from './FocusedCard';
import { HandSidebar } from './HandSidebar';
import { SwipeGesture } from '../features/SwipeGesture';

import type { MeepleCardProps, MobileCardLayoutProps } from '../types';

function cardKey(card: MeepleCardProps, index: number): string {
  return card.id ?? `${card.title}-${index}`;
}

export function MobileCardLayout({
  cards,
  activeId: controlledId,
  onCardSelect,
  className = '',
}: MobileCardLayoutProps) {
  const [internalId, setInternalId] = useState(cards[0] ? cardKey(cards[0], 0) : '');
  const activeId = controlledId ?? internalId;
  const activeIndex = cards.findIndex((c, i) => cardKey(c, i) === activeId);
  const activeCard = activeIndex >= 0 ? cards[activeIndex] : cards[0];

  const selectCard = useCallback(
    (id: string) => {
      setInternalId(id);
      onCardSelect?.(id);
    },
    [onCardSelect]
  );

  const navigateByOffset = useCallback(
    (offset: number) => {
      const newIndex = Math.max(0, Math.min(cards.length - 1, activeIndex + offset));
      const card = cards[newIndex];
      if (card) selectCard(cardKey(card, newIndex));
    },
    [activeIndex, cards, selectCard]
  );

  if (cards.length === 0) return null;

  return (
    <div className={`flex h-full md:hidden ${className}`}>
      <HandSidebar cards={cards} activeId={activeId} onSelect={selectCard} />
      <div className="flex flex-1 flex-col p-1.5 pl-[3px]">
        <SwipeGesture
          onSwipeLeft={() => navigateByOffset(1)}
          onSwipeRight={() => navigateByOffset(-1)}
          className="flex flex-1"
        >
          {activeCard && <FocusedCard {...activeCard} />}
        </SwipeGesture>
      </div>
    </div>
  );
}
