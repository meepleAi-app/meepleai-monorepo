import { memo } from 'react';

import { cn } from '@/lib/utils';

import { getManaDisplayName } from '../mana/mana-config';
import { ManaSymbol } from '../mana/ManaSymbol';
import { entityColors, type MeepleEntityType } from '../meeple-card-styles';

interface ManaBadgeProps {
  entity: MeepleEntityType;
  className?: string;
}

export const ManaBadge = memo(function ManaBadge({ entity, className }: ManaBadgeProps) {
  const color = entityColors[entity].hsl;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 px-2 py-0.5 rounded-md',
        'backdrop-blur-[8px] font-quicksand font-bold',
        'text-[10px] uppercase tracking-wider text-white',
        className
      )}
      style={{ backgroundColor: `hsl(${color} / 0.85)` }}
    >
      <ManaSymbol entity={entity} size="mini" data-testid={`mana-symbol-${entity}`} />
      {getManaDisplayName(entity)}
    </span>
  );
});
