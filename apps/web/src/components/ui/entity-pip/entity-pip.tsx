import type { JSX } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

/**
 * Per-entity solid background (issue #2955). Literal class strings so Tailwind's
 * content scanner emits the utilities (a dynamic `bg-entity-${entity}` would NOT be
 * generated). `kb` uses the registered `-kb` (teal) token — NEVER `-document` (slate),
 * which lives only in `@layer tokens` and is not exposed via `@theme inline`.
 */
const ENTITY_BG: Record<EntityType, string> = {
  game: 'bg-entity-game',
  player: 'bg-entity-player',
  session: 'bg-entity-session',
  agent: 'bg-entity-agent',
  kb: 'bg-entity-kb',
  chat: 'bg-entity-chat',
  event: 'bg-entity-event',
  toolkit: 'bg-entity-toolkit',
  tool: 'bg-entity-tool',
};

/** Per-entity active/focus ring (issue #2955). Same literal-map discipline as ENTITY_BG. */
const ENTITY_RING: Record<EntityType, string> = {
  game: 'ring-entity-game',
  player: 'ring-entity-player',
  session: 'ring-entity-session',
  agent: 'ring-entity-agent',
  kb: 'ring-entity-kb',
  chat: 'ring-entity-chat',
  event: 'ring-entity-event',
  toolkit: 'ring-entity-toolkit',
  tool: 'ring-entity-tool',
};

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

  const hasCount = typeof count === 'number';
  const isEmpty = count === 0;

  // Dot (no count) size classes
  const dotSize = size === 'sm' ? 'h-2 w-2' : 'h-2.5 w-2.5';
  // Pill (with count) size classes
  const pillSize = size === 'sm' ? 'h-4 min-w-4 px-1 text-[10px]' : 'h-5 min-w-5 px-1.5 text-xs';

  const baseClasses = clsx(
    'inline-flex items-center justify-center rounded-full font-medium',
    ENTITY_BG[entity],
    hasCount ? `${pillSize} text-white tabular-nums` : dotSize,
    active && 'ring-2 ring-offset-1',
    active && ENTITY_RING[entity],
    isEmpty && 'opacity-40 cursor-default',
    className
  );

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
      >
        {content}
      </button>
    );
  }

  return (
    <span role="presentation" data-entity={entity} className={baseClasses}>
      {content}
    </span>
  );
}
