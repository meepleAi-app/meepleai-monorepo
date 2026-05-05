import type { CSSProperties, JSX, ReactNode } from 'react';

import clsx from 'clsx';

import type { EntityType } from '../entity-tokens';

// Map EntityType -> CSS variable key. Mirrors TAILWIND_KEY in entity-tokens.ts
// so `kb` resolves to `--e-document` (pre-existing naming from design tokens).
const ENTITY_CSS_VAR_KEY: Record<EntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'document',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
};

export type EntityCardVariant = 'default' | 'elevated' | 'flat';

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
    variant === 'default' && 'border border-border',
    variant === 'elevated' && 'shadow-md',
    isInteractive && 'cursor-pointer transition-transform hover:-translate-y-0.5 hover:bg-muted/40',
    className
  );

  const style: CSSProperties | undefined = entityBorder
    ? { borderLeftColor: `hsl(var(--e-${ENTITY_CSS_VAR_KEY[entity]}))` }
    : undefined;

  if (onClick) {
    return (
      <button
        type="button"
        aria-label={ariaLabel}
        data-entity={entity}
        onClick={onClick}
        className={clsx('block w-full text-left', classes)}
        style={style}
      >
        {children}
      </button>
    );
  }

  return (
    <div data-entity={entity} className={classes} style={style}>
      {children}
    </div>
  );
}
