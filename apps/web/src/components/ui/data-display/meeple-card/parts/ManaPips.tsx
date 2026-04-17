import { entityHsl, entityLabel } from '../tokens';

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
  /** sm = 6px pip no badge; md = 8px pip with count badge; lg = 12px pip with text label */
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
        const isInteractive = pip.onCreate !== undefined;

        // The colored dot with optional md-size count badge
        const dot = (
          <span
            className="relative inline-flex items-center justify-center rounded-full"
            style={{ width: dotSize, height: dotSize, background: color, flexShrink: 0 }}
          >
            {size !== 'sm' && pip.count !== undefined && pip.count > 0 && size !== 'lg' && (
              <span
                className="absolute -top-2 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-1 text-[7px] font-bold text-white"
                style={{ background: color, lineHeight: '10px', minWidth: 12, textAlign: 'center' }}
              >
                {pip.count}
              </span>
            )}
          </span>
        );

        // lg-size label rendered beside the dot
        const lgLabel =
          size === 'lg' ? (
            <span className="ml-1.5 text-[11px] font-semibold" style={{ color }}>
              {pip.count ?? 0} {entityLabel[pip.entityType]}
            </span>
          ) : null;

        const interactiveClasses = isInteractive
          ? ' cursor-pointer transition-transform hover:scale-125 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1'
          : '';

        if (isInteractive) {
          return (
            <button
              key={i}
              type="button"
              data-pip
              aria-label={pip.entityType}
              title={pip.entityType}
              className={'inline-flex items-center' + interactiveClasses}
              onClick={() => pip.onCreate!()}
            >
              {dot}
              {lgLabel}
            </button>
          );
        }

        return (
          <span key={i} data-pip title={pip.entityType} className="inline-flex items-center">
            {dot}
            {lgLabel}
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
