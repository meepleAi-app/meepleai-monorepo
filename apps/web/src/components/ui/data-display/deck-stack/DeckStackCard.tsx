import { memo } from 'react';

import { cn } from '@/lib/utils';

import { ManaSymbol } from '../mana/ManaSymbol';
import { entityColors } from '../meeple-card-styles';

import type { DeckStackItem } from './deck-stack-types';

interface DeckStackCardProps {
  item: DeckStackItem;
  index: number;
  onClick: (id: string, entityType: string) => void;
  className?: string;
}

// Fan rotation: cards spread from -8° to +8°, centered at 0
function getRotation(index: number, total: number = 5): number {
  if (total <= 1) return 0;
  const range = 16; // -8 to +8
  return -8 + (index / (total - 1)) * range;
}

export const DeckStackCard = memo(function DeckStackCard({
  item,
  index,
  onClick,
  className,
}: DeckStackCardProps) {
  const color = entityColors[item.entityType].hsl;

  return (
    <div
      onClick={() => onClick(item.id, item.entityType)}
      className={cn(
        'flex items-center gap-2 px-2.5 py-1.5 rounded-lg cursor-pointer',
        'w-[120px] h-[48px] border border-white/10',
        'transition-transform duration-150 hover:-translate-y-2 hover:scale-105',
        className
      )}
      style={{
        backgroundColor: `hsl(${color} / 0.05)`,
        borderLeft: `3px solid hsl(${color})`,
        transform: `rotate(${getRotation(index)}deg)`,
      }}
    >
      <ManaSymbol entity={item.entityType} size="mini" />
      <div className="flex-1 min-w-0">
        <div className="text-[11px] text-slate-200 font-medium truncate">{item.title}</div>
        {item.status && (
          <div data-testid="deck-stack-card-status" className="text-[9px] text-slate-400">
            {item.status}
          </div>
        )}
      </div>
    </div>
  );
});
