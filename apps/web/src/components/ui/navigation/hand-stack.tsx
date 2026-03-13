/**
 * HandStack - Left-edge mini-card stack for mobile card-focus layout
 *
 * Displays up to 7 mini-cards vertically. Each mini-card shows the entity
 * icon + truncated title with entity-colored accent. The focused card is
 * highlighted.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import type { HandCard } from '@/hooks/use-hand-context';
import { cn } from '@/lib/utils';

interface HandStackProps {
  cards: HandCard[];
  focusedIdx: number;
  onCardClick: (index: number) => void;
  className?: string;
}

export function HandStack({ cards, focusedIdx, onCardClick, className }: HandStackProps) {
  if (cards.length === 0) return null;

  return (
    <div
      className={cn(
        'flex flex-col gap-1.5 py-2 px-1.5',
        'w-14 shrink-0',
        'overflow-y-auto scrollbar-none',
        className
      )}
      data-testid="hand-stack"
    >
      {cards.map((card, i) => {
        const Icon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
        const hsl = entityColors[card.entity]?.hsl ?? '220 70% 50%';
        const isFocused = i === focusedIdx;

        return (
          <button
            key={card.id}
            type="button"
            onClick={() => onCardClick(i)}
            className={cn(
              'flex flex-col items-center gap-0.5 p-1.5 rounded-lg',
              'transition-all duration-200 cursor-pointer',
              'border',
              isFocused
                ? 'bg-[hsl(var(--hand-hsl)/0.12)] border-[hsl(var(--hand-hsl)/0.4)] scale-105'
                : 'bg-card/60 border-border/30 hover:bg-card/80'
            )}
            style={{ '--hand-hsl': hsl } as React.CSSProperties}
            data-testid={`hand-stack-item-${card.id}`}
            data-focused={isFocused ? 'true' : 'false'}
            title={card.title}
          >
            <Icon
              className={cn(
                'w-4 h-4',
                isFocused ? 'text-[hsl(var(--hand-hsl))]' : 'text-muted-foreground'
              )}
            />
            <span
              className={cn(
                'text-[8px] font-nunito font-bold truncate w-full text-center leading-tight',
                isFocused ? 'text-[hsl(var(--hand-hsl))]' : 'text-muted-foreground/70'
              )}
            >
              {card.title}
            </span>
          </button>
        );
      })}
    </div>
  );
}
