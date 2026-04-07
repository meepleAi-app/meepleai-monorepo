'use client';

import { useState, useCallback } from 'react';
import { HandSidebar } from './HandSidebar';
import { FocusedCard } from './FocusedCard';
import { SwipeGesture } from '../features/SwipeGesture';
import type { MobileCardLayoutProps } from '../types';

export function MobileCardLayout({ cards, activeId: controlledId, onCardSelect, className = '' }: MobileCardLayoutProps) {
  const [internalId, setInternalId] = useState(cards[0]?.id ?? cards[0]?.title ?? '');
  const activeId = controlledId ?? internalId;
  const activeCard = cards.find((c) => (c.id ?? c.title) === activeId) ?? cards[0];
  const activeIndex = cards.findIndex((c) => (c.id ?? c.title) === activeId);

  const selectCard = useCallback((id: string) => {
    setInternalId(id);
    onCardSelect?.(id);
  }, [onCardSelect]);

  const navigateByOffset = useCallback((offset: number) => {
    const newIndex = Math.max(0, Math.min(cards.length - 1, activeIndex + offset));
    const card = cards[newIndex];
    if (card) selectCard(card.id ?? card.title);
  }, [activeIndex, cards, selectCard]);

  if (cards.length === 0) return null;

  return (
    <div className={`flex h-full md:hidden ${className}`}>
      <HandSidebar cards={cards} activeId={activeId} onSelect={selectCard} />
      <div className="flex flex-1 flex-col p-1.5 pl-[3px]">
        <SwipeGesture onSwipeLeft={() => navigateByOffset(1)} onSwipeRight={() => navigateByOffset(-1)} className="flex flex-1">
          {activeCard && <FocusedCard {...activeCard} />}
        </SwipeGesture>
      </div>
    </div>
  );
}
