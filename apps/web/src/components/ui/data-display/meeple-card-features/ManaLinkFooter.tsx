import { memo, useCallback, type MouseEvent } from 'react';

import { useCascadeNavigationStore } from '@/lib/stores/cascadeNavigationStore';
import { cn } from '@/lib/utils';

import { ManaSymbol } from '../mana/ManaSymbol';

import type { MeepleEntityType } from '../meeple-card-styles';

export interface LinkedEntityInfo {
  entityType: MeepleEntityType;
  count: number;
}

interface ManaLinkFooterProps {
  linkedEntities: LinkedEntityInfo[];
  onPipClick?: (entityType: MeepleEntityType) => void;
  sourceEntityId?: string;
  maxVisible?: number;
  className?: string;
}

export const ManaLinkFooter = memo(function ManaLinkFooter({
  linkedEntities,
  onPipClick,
  sourceEntityId,
  maxVisible = 4,
  className,
}: ManaLinkFooterProps) {
  const openDeckStack = useCascadeNavigationStore(s => s.openDeckStack);

  const handlePipClick = useCallback(
    (entityType: MeepleEntityType, event: MouseEvent) => {
      if (onPipClick) {
        onPipClick(entityType);
        return;
      }
      const anchorRect = event.currentTarget.getBoundingClientRect();
      openDeckStack(entityType, sourceEntityId ?? '', anchorRect);
    },
    [onPipClick, openDeckStack, sourceEntityId]
  );

  if (linkedEntities.length === 0) return null;

  const visible = linkedEntities.slice(0, maxVisible);
  const overflow = linkedEntities.length - maxVisible;

  return (
    <div
      className={cn('flex items-center gap-1.5 px-3.5 py-1.5 border-t border-white/5', className)}
    >
      {visible.map(({ entityType }) => (
        <span
          key={entityType}
          onClick={e => handlePipClick(entityType, e)}
          className="cursor-pointer"
        >
          <ManaSymbol
            entity={entityType}
            size="mini"
            data-testid={`mana-pip-${entityType}`}
            className="transition-transform duration-150 hover:scale-125 pointer-events-none"
          />
        </span>
      ))}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-slate-500 ml-auto">+{overflow}</span>
      )}
    </div>
  );
});
