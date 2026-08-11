import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

export type EntityCardVariant = 'default' | 'elevated' | 'flat';

/**
 * Per-entity left-border accent (issue #2955). Literal class strings so Tailwind's
 * content scanner generates the utilities (dynamic `border-l-entity-${e}` would not
 * be emitted). `kb` uses the registered `-kb` (teal) token — NOT `-document` (slate),
 * which lives only in `@layer tokens` and is not exposed via `@theme inline`.
 */
const ENTITY_BORDER_L: Record<EntityType, string> = {
  game: 'border-l-entity-game',
  player: 'border-l-entity-player',
  session: 'border-l-entity-session',
  agent: 'border-l-entity-agent',
  kb: 'border-l-entity-kb',
  chat: 'border-l-entity-chat',
  event: 'border-l-entity-event',
  toolkit: 'border-l-entity-toolkit',
  tool: 'border-l-entity-tool',
};

export interface EntityCardProps {
  readonly entity: EntityType;
  readonly variant?: EntityCardVariant;
  readonly interactive?: boolean;
  readonly onClick?: () => void;
  readonly ariaLabel?: string;
  readonly className?: string;
  readonly entityBorder?: boolean;
  readonly children: ReactNode;
}

export function EntityCard({
  entity,
  variant = 'default',
  interactive = false,
  onClick,
  ariaLabel,
  className,
  entityBorder = true,
  children,
}: EntityCardProps): JSX.Element {
  if (onClick && !ariaLabel) {
    throw new Error(
      'EntityCard: `ariaLabel` is required when `onClick` is provided for accessibility.'
    );
  }

  const isInteractive = interactive || Boolean(onClick);

  const classes = clsx(
    'bg-card rounded-xl p-4 text-foreground transition-colors',
    entityBorder && 'border-l-4',
    entityBorder && ENTITY_BORDER_L[entity],
    variant === 'default' && 'border border-border',
    variant === 'elevated' && 'shadow-md',
    isInteractive && 'cursor-pointer transition-transform hover:-translate-y-0.5 hover:bg-muted/40',
    className
  );

  if (onClick) {
    return (
      <button
        type="button"
        aria-label={ariaLabel}
        data-entity={entity}
        onClick={onClick}
        className={clsx('block w-full text-left', classes)}
      >
        {children}
      </button>
    );
  }

  return (
    <div data-entity={entity} className={classes}>
      {children}
    </div>
  );
}
