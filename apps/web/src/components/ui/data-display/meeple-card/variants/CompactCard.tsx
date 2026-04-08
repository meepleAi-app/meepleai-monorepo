'use client';

import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function CompactCard(props: MeepleCardProps) {
  const { entity, title, onClick, className = '' } = props;
  const testId = props['data-testid'];

  return (
    <div
      className={`group flex items-center gap-2 rounded-lg border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-2.5 py-2 shadow-[var(--mc-shadow-sm)] transition-transform duration-200 hover:scale-[1.02] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-testid={testId}
    >
      <div
        className="h-1.5 w-1.5 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />
      <span className="truncate font-[var(--font-quicksand)] text-[0.85rem] font-semibold text-[var(--mc-text-primary)]">
        {title}
      </span>
    </div>
  );
}
