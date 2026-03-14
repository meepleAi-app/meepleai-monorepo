'use client';

import { useCallback } from 'react';

import { X, Pin } from 'lucide-react';

import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { cn } from '@/lib/utils';
import type { HandCard } from '@/stores/use-card-hand';

type CardLevel = 'mini' | 'card';

interface CardStackItemProps {
  card: HandCard;
  level: CardLevel;
  index: number;
  isFocused: boolean;
  isPinned: boolean;
  onFocus: (index: number) => void;
  onDiscard: (id: string) => void;
}

export function CardStackItem({
  card,
  level,
  index,
  isFocused,
  isPinned,
  onFocus,
  onDiscard,
}: CardStackItemProps) {
  const Icon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
  const hsl = entityColors[card.entity]?.hsl ?? '220 70% 50%';

  const handleClick = useCallback(() => onFocus(index), [onFocus, index]);

  const handleDiscard = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      onDiscard(card.id);
    },
    [onDiscard, card.id]
  );

  if (level === 'mini') {
    return (
      <button
        type="button"
        onClick={handleClick}
        className={cn(
          'flex flex-col items-center gap-0.5 p-1.5 rounded-lg',
          'transition-all duration-200 cursor-pointer border',
          isFocused
            ? 'bg-[hsl(var(--card-hsl)/0.12)] border-[hsl(var(--card-hsl)/0.4)] scale-105'
            : 'bg-card/60 border-border/30 hover:bg-card/80'
        )}
        style={{ '--card-hsl': hsl } as React.CSSProperties}
        data-testid={`card-stack-item-${card.id}`}
        data-focused={isFocused ? 'true' : 'false'}
        title={card.title}
      >
        <Icon
          className={cn(
            'w-4 h-4',
            isFocused ? 'text-[hsl(var(--card-hsl))]' : 'text-muted-foreground'
          )}
        />
        {isPinned && <Pin className="w-2 h-2 text-muted-foreground/50" aria-label="Pinned" />}
      </button>
    );
  }

  // Card level
  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        'group flex items-center gap-3 px-3 py-2.5 rounded-lg w-full text-left',
        'transition-all duration-200 cursor-pointer border',
        isFocused
          ? 'bg-[hsl(var(--card-hsl)/0.1)] border-[hsl(var(--card-hsl)/0.25)] shadow-sm'
          : 'bg-card/50 border-border/30 hover:bg-[hsl(var(--card-hsl)/0.06)] hover:border-[hsl(var(--card-hsl)/0.2)]'
      )}
      style={{ '--card-hsl': hsl } as React.CSSProperties}
      data-testid={`card-stack-item-${card.id}`}
      data-focused={isFocused ? 'true' : 'false'}
    >
      {/* Entity icon */}
      <div
        className={cn(
          'flex items-center justify-center w-8 h-8 rounded-md shrink-0',
          'bg-[hsl(var(--card-hsl)/0.12)]'
        )}
      >
        <Icon className="w-4 h-4 text-[hsl(var(--card-hsl))]" />
      </div>

      {/* Label + subtitle */}
      <div className="flex-1 min-w-0">
        <p
          className={cn(
            'text-sm font-medium truncate font-quicksand',
            isFocused ? 'text-[hsl(var(--card-hsl))]' : 'text-foreground'
          )}
        >
          {card.title}
        </p>
        {card.subtitle && (
          <p className="text-[10px] text-muted-foreground truncate font-nunito">{card.subtitle}</p>
        )}
      </div>

      {/* Pin indicator */}
      {isPinned && (
        <Pin className="w-3 h-3 text-muted-foreground/50 shrink-0" aria-label="Pinned" />
      )}

      {/* Discard button (visible on hover) */}
      <button
        type="button"
        onClick={handleDiscard}
        className={cn(
          'flex items-center justify-center w-5 h-5 rounded shrink-0',
          'text-muted-foreground/0 group-hover:text-muted-foreground/70',
          'hover:!text-destructive hover:bg-destructive/10',
          'transition-colors'
        )}
        aria-label="Discard card"
      >
        <X className="w-3 h-3" />
      </button>
    </button>
  );
}
