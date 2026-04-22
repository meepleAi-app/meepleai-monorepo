import type { JSX } from 'react';

import clsx from 'clsx';

import { getEntityToken, type EntityType } from '../entity-tokens';

export interface EntityPipProps {
  readonly entity: EntityType;
  readonly count?: number;
  readonly active?: boolean;
  readonly size?: 'sm' | 'md';
  readonly onClick?: () => void;
  readonly ariaLabel?: string;
  readonly className?: string;
}

export function EntityPip({
  entity,
  count,
  active = false,
  size = 'sm',
  onClick,
  ariaLabel,
  className,
}: EntityPipProps): JSX.Element {
  if (onClick && !ariaLabel) {
    throw new Error(
      'EntityPip: `ariaLabel` is required when `onClick` is provided for accessibility.'
    );
  }

  const token = getEntityToken(entity);
  const hasCount = typeof count === 'number';
  const isEmpty = count === 0;

  // Dot (no count) size classes
  const dotSize = size === 'sm' ? 'h-2 w-2' : 'h-2.5 w-2.5';
  // Pill (with count) size classes
  const pillSize = size === 'sm' ? 'h-4 min-w-4 px-1 text-[10px]' : 'h-5 min-w-5 px-1.5 text-xs';

  const tailwindKey = token.bg.replace('bg-entity-', '');

  const baseClasses = clsx(
    'inline-flex items-center justify-center rounded-full font-medium',
    token.bg,
    hasCount ? `${pillSize} text-white tabular-nums` : dotSize,
    active && 'ring-2 ring-offset-1',
    isEmpty && 'opacity-40 cursor-default',
    className
  );

  // Use entity color at 0.35 alpha for ring via CSS variable in globals.css
  const ringStyle = active
    ? { boxShadow: `0 0 0 2px hsl(var(--e-${tailwindKey}) / 0.35)` }
    : undefined;

  const content = hasCount ? count : null;

  if (onClick) {
    return (
      <button
        type="button"
        aria-label={ariaLabel}
        data-entity={entity}
        onClick={onClick}
        disabled={isEmpty}
        className={baseClasses}
        style={ringStyle}
      >
        {content}
      </button>
    );
  }

  return (
    <span role="presentation" data-entity={entity} className={baseClasses} style={ringStyle}>
      {content}
    </span>
  );
}
