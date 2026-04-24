'use client';

import { ManaPips } from '../parts/ManaPips';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

export function CompactCard(props: MeepleCardProps) {
  const { entity, title, badge, manaPips, onClick, className = '' } = props;
  const testId = props['data-testid'];

  // S10 documented no-op: CompactCard does not render `connections`.
  // The dense list-item layout has no room for a chip strip; navigation
  // affordances live on the parent container. If the caller passes
  // `connections`, we silently ignore to preserve layout invariants.

  return (
    <div
      className={`group flex items-center gap-2 rounded-lg border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-2.5 py-2 shadow-[var(--mc-shadow-sm)] transition-transform duration-200 hover:scale-[1.02] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      <div
        className="h-1.5 w-1.5 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />
      <span className="truncate font-[var(--font-quicksand)] text-[0.85rem] font-semibold text-[var(--mc-text-primary)]">
        {title}
      </span>
      {badge && (
        <span
          className="ml-auto shrink-0 rounded-full bg-black/10 px-1.5 py-0.5 text-[8px] font-bold text-[var(--mc-text-primary)] dark:bg-white/15"
          data-slot="badge"
        >
          {badge}
        </span>
      )}
      {manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="sm" />}
    </div>
  );
}
