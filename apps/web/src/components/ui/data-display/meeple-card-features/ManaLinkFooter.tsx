import { memo } from 'react';

import { cn } from '@/lib/utils';

import { ManaSymbol } from '../mana/ManaSymbol';
import type { MeepleEntityType } from '../meeple-card-styles';

export interface LinkedEntityInfo {
  entityType: MeepleEntityType;
  count: number;
}

interface ManaLinkFooterProps {
  linkedEntities: LinkedEntityInfo[];
  onPipClick: (entityType: MeepleEntityType) => void;
  maxVisible?: number;
  className?: string;
}

export const ManaLinkFooter = memo(function ManaLinkFooter({
  linkedEntities,
  onPipClick,
  maxVisible = 4,
  className,
}: ManaLinkFooterProps) {
  if (linkedEntities.length === 0) return null;

  const visible = linkedEntities.slice(0, maxVisible);
  const overflow = linkedEntities.length - maxVisible;

  return (
    <div className={cn('flex items-center gap-1.5 px-3.5 py-1.5 border-t border-white/5', className)}>
      {visible.map(({ entityType }) => (
        <ManaSymbol
          key={entityType}
          entity={entityType}
          size="mini"
          onClick={() => onPipClick(entityType)}
          data-testid={`mana-pip-${entityType}`}
          className="transition-transform duration-150 hover:scale-125"
        />
      ))}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-slate-500 ml-auto">+{overflow}</span>
      )}
    </div>
  );
});
