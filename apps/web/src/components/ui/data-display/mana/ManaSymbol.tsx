import { memo } from 'react';

import { cn } from '@/lib/utils';

import { entityColors, type MeepleEntityType } from '../meeple-card-styles';
import { MANA_DISPLAY } from './mana-config';

import type { ManaSize } from './mana-types';

const SIZE_CLASSES: Record<ManaSize, string> = {
  full: 'w-16 h-16 text-[1.6rem]',
  medium: 'w-7 h-7 text-sm',
  mini: 'w-5 h-5 text-xs',
};

const LABEL_CLASSES: Record<ManaSize, string> = {
  full: 'text-xs mt-2',
  medium: 'text-[9px] ml-1.5',
  mini: 'text-[8px] ml-1',
};

interface ManaSymbolProps {
  entity: MeepleEntityType;
  size?: ManaSize;
  showLabel?: boolean;
  customColor?: string;
  className?: string;
  onClick?: () => void;
  'data-testid'?: string;
}

export const ManaSymbol = memo(function ManaSymbol({
  entity,
  size = 'full',
  showLabel = false,
  customColor,
  className,
  onClick,
  'data-testid': dataTestId,
  ...props
}: ManaSymbolProps) {
  const config = MANA_DISPLAY[entity];
  const color = customColor ?? entityColors[entity].hsl;
  const testId = dataTestId ?? `mana-symbol-${entity}`;

  return (
    <span
      className={cn(
        'inline-flex items-center',
        showLabel && size !== 'full' && 'flex-row',
        showLabel && size === 'full' && 'flex-col items-center',
        onClick && 'cursor-pointer',
        className
      )}
    >
      <span
        data-testid={testId}
        onClick={onClick}
        className={cn(
          'inline-flex items-center justify-center rounded-full relative',
          SIZE_CLASSES[size],
          onClick && 'transition-transform duration-150 hover:scale-110'
        )}
        style={
          {
            '--mana-color': color,
            background: `radial-gradient(circle at 35% 35%, hsl(${color}) 0%, hsl(${color} / 0.65) 100%)`,
            boxShadow: `0 4px 16px hsl(${color} / 0.35)`,
            outline: `2px solid hsl(${color} / 0.35)`,
            outlineOffset: '2px',
          } as React.CSSProperties
        }
      >
        <span
          className="absolute inset-[3px] rounded-full border border-white/[0.08]"
          aria-hidden="true"
        />
        <span className="relative z-[1] drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
          {config.symbol}
        </span>
      </span>
      {showLabel && (
        <span
          className={cn('font-quicksand font-bold uppercase tracking-wider', LABEL_CLASSES[size])}
          style={{ color: `hsl(${color})` }}
        >
          {config.displayName}
        </span>
      )}
    </span>
  );
});
