import { entityHsl, entityLabel } from '../tokens';
import type { MeepleEntityType } from '../types';

interface EntityBadgeProps {
  entity: MeepleEntityType;
  className?: string;
}

export function EntityBadge({ entity, className = '' }: EntityBadgeProps) {
  return (
    <span
      className={`absolute left-2.5 top-2 z-10 rounded-md px-2 py-0.5 font-[var(--font-quicksand)] text-[9px] font-extrabold uppercase tracking-wide text-white shadow-sm ${className}`}
      style={{ background: entityHsl(entity) }}
    >
      {entityLabel[entity]}
    </span>
  );
}
