import { entityHsl } from '../tokens';

import type { MeepleEntityType } from '../types';

interface TagStripProps {
  tags: string[];
  entity: MeepleEntityType;
  maxVisible?: number;
  /**
   * Tailwind top-* class controlling vertical position. Defaults to `top-8`
   * (legacy). Pass a higher value (e.g. `top-14`) when the host card stacks
   * EntityBadge + StatusBadge above the strip to avoid overlap.
   */
  topClass?: string;
}

export function TagStrip({ tags, entity, maxVisible = 3, topClass = 'top-8' }: TagStripProps) {
  if (tags.length === 0) return null;

  const visible = tags.slice(0, maxVisible);
  const overflow = tags.length - maxVisible;

  return (
    <div className={`absolute left-2 ${topClass} z-[9] flex flex-col gap-1`}>
      {visible.map((tag, i) => (
        <span
          key={i}
          className="rounded px-1.5 py-[1px] text-[9px] font-semibold"
          style={{
            background: entityHsl(entity, 0.12),
            color: entityHsl(entity),
          }}
        >
          {tag}
        </span>
      ))}
      {overflow > 0 && (
        <span className="text-[8px] font-bold text-[var(--mc-text-muted)]">+{overflow}</span>
      )}
    </div>
  );
}
