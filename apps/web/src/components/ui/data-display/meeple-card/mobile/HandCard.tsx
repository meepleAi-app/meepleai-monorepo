'use client';

import { entityHsl, entityIcon } from '../tokens';

import type { MeepleEntityType } from '../types';

interface HandCardProps {
  entity: MeepleEntityType;
  title: string;
  isActive: boolean;
  onClick: () => void;
}

export function HandCard({ entity, title, isActive, onClick }: HandCardProps) {
  const color = entityHsl(entity);
  return (
    <button
      onClick={onClick}
      className={`relative h-10 w-7 flex-shrink-0 cursor-pointer overflow-hidden rounded-md border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] transition-transform duration-200 hover:scale-110 ${isActive ? 'scale-[1.12] outline outline-2 outline-offset-1' : ''}`}
      style={isActive ? { outlineColor: color } : undefined}
    >
      <div className="absolute bottom-0 left-0 top-0 w-[3px]" style={{ background: color }} />
      <div className="flex h-[22px] w-full items-center justify-center text-[11px]">
        {entityIcon[entity]}
      </div>
      <span className="absolute bottom-[1px] left-[3px] font-[var(--font-quicksand)] text-[4px] font-bold">
        {title.slice(0, 6)}
      </span>
    </button>
  );
}
