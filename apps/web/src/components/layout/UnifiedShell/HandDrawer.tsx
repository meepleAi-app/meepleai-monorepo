'use client';

import { useCardHand } from '@/stores/use-card-hand';

import { HandDrawerCard } from './HandDrawerCard';

export function HandDrawer() {
  const { cards, focusedIdx, isHandCollapsed, focusCard, maxHandSize, collapseHand } =
    useCardHand();

  if (cards.length === 0 || isHandCollapsed) {
    return null;
  }

  return (
    <nav
      role="navigation"
      aria-label="Carte in mano"
      className="bg-white/55 backdrop-blur-[12px] border-b border-[rgba(180,130,80,0.1)] px-2.5 py-1 pb-1.5"
    >
      {/* Grip handle */}
      <button
        type="button"
        aria-label="Espandi/comprimi mano"
        onClick={() => collapseHand()}
        className="mx-auto mb-1.5 block cursor-pointer border-none bg-transparent p-0"
        style={{ width: 36, height: 4, borderRadius: 2, background: 'rgba(180,130,80,0.2)' }}
      />

      {/* Horizontal scroll area */}
      <div
        className="flex gap-1.5 overflow-x-auto"
        style={{
          scrollbarWidth: 'none',
          msOverflowStyle: 'none',
        }}
      >
        {cards.map((card, idx) => (
          <HandDrawerCard
            key={card.id}
            card={card}
            isFocused={idx === focusedIdx}
            onClick={() => focusCard(idx)}
          />
        ))}
      </div>

      {/* Card count */}
      <div className="text-[8px] text-black/20 font-quicksand text-center mt-0.5">
        {cards.length}/{maxHandSize}
      </div>
    </nav>
  );
}
