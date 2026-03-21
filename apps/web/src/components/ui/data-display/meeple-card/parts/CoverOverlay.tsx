'use client';

import { memo } from 'react';

import { cn } from '@/lib/utils';

import { ManaSymbol } from '../../mana/ManaSymbol';
import { entityColors, type MeepleEntityType } from '../../meeple-card-styles';

import type { CoverLabel, SubtypeIcon } from '../types';

export interface CoverOverlayProps {
  entity: MeepleEntityType;
  customColor?: string;
  /** Top-left: label stack */
  coverLabels?: CoverLabel[];
  /** Top-right: show entity type mana pip */
  showEntityType?: boolean;
  /** Bottom-left: subtype classification icons */
  subtypeIcons?: SubtypeIcon[];
  /** Bottom-left: legacy single icon (backward compat, maps to subtypeIcons[0]) */
  mechanicIcon?: React.ReactNode;
  /** Bottom-right: state badge */
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
}

const STATE_COLORS = {
  success: 'bg-emerald-500/80 text-white',
  warning: 'bg-amber-500/80 text-black',
  error: 'bg-red-500/80 text-white',
  info: 'bg-blue-500/80 text-white',
} as const;

export const CoverOverlay = memo(function CoverOverlay({
  entity,
  customColor,
  coverLabels,
  showEntityType,
  subtypeIcons,
  mechanicIcon,
  stateLabel,
}: CoverOverlayProps) {
  const color = customColor ?? entityColors[entity].hsl;

  // Backward compat: mechanicIcon → subtypeIcons[0]
  const effectiveSubtypes =
    subtypeIcons ?? (mechanicIcon ? [{ icon: mechanicIcon, tooltip: '' }] : undefined);

  const hasContent =
    (coverLabels && coverLabels.length > 0) ||
    showEntityType ||
    (effectiveSubtypes && effectiveSubtypes.length > 0) ||
    stateLabel;
  if (!hasContent) return null;

  return (
    <div className="absolute inset-0 z-[3] pointer-events-none" data-testid="cover-overlay">
      {/* Top-Left: Label Stack */}
      {coverLabels && coverLabels.length > 0 && (
        <div
          className="pointer-events-auto absolute top-2 left-2 flex flex-col gap-1 max-w-[calc(100%-50px)]"
          data-testid="cover-labels"
        >
          {coverLabels.map((label, i) => (
            <span
              key={i}
              data-testid={`cover-label-${i}`}
              className={cn(
                'inline-flex items-center gap-1 rounded-[5px]',
                'backdrop-blur-[8px] font-quicksand font-bold text-white',
                'text-shadow-sm overflow-hidden text-ellipsis whitespace-nowrap w-fit',
                label.primary
                  ? 'text-[11px] px-[9px] py-[3px]'
                  : 'text-[9px] px-2 py-[2px] opacity-90'
              )}
              style={{
                backgroundColor: `hsl(${label.color ?? color} / ${label.primary ? 0.85 : 0.75})`,
              }}
            >
              {label.text}
            </span>
          ))}
        </div>
      )}

      {/* Top-Right: Entity Type Pip */}
      {showEntityType && (
        <div className="pointer-events-auto absolute top-2 right-2" data-testid="cover-entity-type">
          <ManaSymbol
            entity={entity}
            size="mini"
            customColor={customColor}
            data-testid={`mana-symbol-${entity}`}
          />
        </div>
      )}

      {/* Bottom-Left: Subtype Icons */}
      {effectiveSubtypes && effectiveSubtypes.length > 0 && (
        <div
          className="pointer-events-auto absolute bottom-2 left-2 flex gap-1"
          data-testid="cover-subtypes"
        >
          {effectiveSubtypes.map((sub, i) => (
            <span
              key={i}
              title={sub.tooltip}
              className={cn(
                'flex items-center justify-center',
                'w-6 h-6 rounded-md',
                'backdrop-blur-[8px] bg-black/45',
                'border border-white/[0.12]',
                'text-xs cursor-pointer',
                'transition-transform duration-150 hover:scale-[1.15] hover:bg-black/60'
              )}
            >
              {sub.icon}
            </span>
          ))}
        </div>
      )}

      {/* Bottom-Right: State Label */}
      {stateLabel && (
        <span
          className={cn(
            'pointer-events-auto absolute bottom-2 right-2',
            'px-[9px] py-[3px] rounded-md',
            'backdrop-blur-[8px]',
            'font-quicksand font-bold text-[9px] uppercase tracking-wider',
            STATE_COLORS[stateLabel.variant]
          )}
          data-testid="cover-state-label"
        >
          {stateLabel.text}
        </span>
      )}
    </div>
  );
});
