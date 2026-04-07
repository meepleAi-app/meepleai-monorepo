'use client';

import { HandCard } from './HandCard';
import type { MeepleCardProps } from '../types';

interface HandSidebarProps {
  cards: MeepleCardProps[];
  activeId?: string;
  onSelect: (id: string) => void;
}

export function HandSidebar({ cards, activeId, onSelect }: HandSidebarProps) {
  return (
    <div className="flex w-9 flex-shrink-0 flex-col items-center gap-[5px] bg-gradient-to-r from-black/[0.02] to-transparent py-1.5">
      <span className="mb-1 font-[var(--font-quicksand)] text-[7px] font-bold uppercase tracking-widest text-[var(--mc-text-muted)] [writing-mode:vertical-rl] [transform:rotate(180deg)]">
        Cards
      </span>
      {cards.map((card, i) => {
        const key = card.id ?? `${card.title}-${i}`;
        return (
          <HandCard
            key={key}
            entity={card.entity}
            title={card.title}
            isActive={key === activeId}
            onClick={() => onSelect(key)}
          />
        );
      })}
    </div>
  );
}
