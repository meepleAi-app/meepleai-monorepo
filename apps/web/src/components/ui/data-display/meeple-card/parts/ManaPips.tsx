import { entityHsl } from '../tokens';

import type { MeepleEntityType } from '../types';

export interface ManaPipItem {
  id: string;
  label: string;
  href: string;
}

export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  items?: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
}

interface ManaPipsProps {
  pips: ManaPip[];
  /** sm = 6px pip no badge (compact); md = 8px pip with count badge (grid); lg = 12px pip with label (hero) */
  size?: 'sm' | 'md' | 'lg';
}

const MAX_VISIBLE = 3;

export function ManaPips({ pips, size = 'md' }: ManaPipsProps) {
  if (pips.length === 0) return null;

  const visible = pips.slice(0, MAX_VISIBLE);
  const overflow = pips.length - MAX_VISIBLE;
  const dotSize = size === 'lg' ? 12 : size === 'md' ? 8 : 6;

  return (
    <div className="flex items-center gap-1 px-3 pb-2 pt-0.5">
      {visible.map((pip, i) => {
        const color = entityHsl(pip.entityType);
        return (
          <span
            key={i}
            data-pip
            title={pip.entityType}
            className="relative inline-flex items-center justify-center rounded-full"
            style={{
              width: dotSize,
              height: dotSize,
              background: color,
              flexShrink: 0,
            }}
          >
            {size !== 'sm' && pip.count !== undefined && pip.count > 0 && (
              <span
                className="absolute -top-2 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-1 text-[7px] font-bold text-white"
                style={{ background: color, lineHeight: '10px', minWidth: 12, textAlign: 'center' }}
              >
                {pip.count}
              </span>
            )}
          </span>
        );
      })}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-[var(--mc-text-muted,#94a3b8)]">
          +{overflow}
        </span>
      )}
    </div>
  );
}
