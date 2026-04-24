import { entityHsl, entityLabel } from '../tokens';

import type { MeepleEntityType } from '../types';

interface EntityBadgeProps {
  entity: MeepleEntityType;
  className?: string;
  /**
   * When true, renders without absolute positioning (no top/left).
   * Used when the badge is wrapped in an external flex stack container
   * (e.g. GridCard's BadgeStack). Default: false (legacy absolute positioning).
   */
  stacked?: boolean;
}

export function EntityBadge({ entity, className = '', stacked = false }: EntityBadgeProps) {
  const positioning = stacked ? 'self-start' : 'absolute left-2.5 top-2 z-10';
  return (
    <span
      className={`${positioning} rounded-md px-2 py-0.5 font-[var(--font-quicksand)] text-[9px] font-extrabold uppercase tracking-wide text-white shadow-sm ${className}`}
      style={{ background: entityHsl(entity) }}
    >
      {entityLabel[entity]}
    </span>
  );
}
