'use client';

import type { HandCard } from '@/stores/use-card-hand';

import { ENTITY_COLORS, ENTITY_EMOJIS } from './entity-hand-constants';

interface NavbarMiniCardsProps {
  cards: HandCard[];
  onExpand: (cardId: string) => void;
}

export function NavbarMiniCards({ cards, onExpand }: NavbarMiniCardsProps) {
  return (
    <div className="flex items-center gap-0.5">
      {cards.map(card => (
        <button
          key={card.id}
          type="button"
          aria-label={card.title}
          className="min-w-[44px] min-h-[44px] flex items-center justify-center opacity-70 hover:opacity-100 transition-opacity"
          onClick={() => onExpand(card.id)}
        >
          {card.imageUrl ? (
            <img src={card.imageUrl} alt="" className="w-5 h-5 rounded-full object-cover" />
          ) : (
            <span
              className="w-5 h-5 rounded-full flex items-center justify-center text-[10px]"
              style={{
                backgroundColor: `hsl(${ENTITY_COLORS[card.entity] ?? ENTITY_COLORS.custom})`,
              }}
            >
              {ENTITY_EMOJIS[card.entity] ?? ENTITY_EMOJIS.custom}
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
