import type { JSX, MouseEvent, ReactNode } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

/**
 * Per-entity color maps (issue #2955). Literal class strings so Tailwind's content
 * scanner emits the utilities (a dynamic `border-l-entity-${entity}` would NOT be
 * generated). `kb` uses the registered `-kb` (teal) token — NEVER `-document` (slate),
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

export interface NotificationCardProps {
  readonly entity: EntityType;
  readonly title: string;
  readonly body?: string;
  readonly timestamp: string;
  readonly unread?: boolean;
  readonly icon?: ReactNode;
  readonly onClick?: () => void;
  readonly onDismiss?: () => void;
  readonly dismissAriaLabel?: string;
  readonly className?: string;
}

function DismissIcon(): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      width="14"
      height="14"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}

export function NotificationCard({
  entity,
  title,
  body,
  timestamp,
  unread = false,
  icon,
  onClick,
  onDismiss,
  dismissAriaLabel = 'Rimuovi notifica',
  className,
}: NotificationCardProps): JSX.Element {
  const containerClasses = clsx(
    'group relative flex gap-3 rounded-xl border-l-4 bg-card p-4 text-foreground transition-colors',
    ENTITY_BORDER_L[entity],
    unread && 'bg-muted/20',
    onClick && 'w-full cursor-pointer text-left hover:bg-muted/40',
    className
  );

  const titleClasses = clsx(
    'text-sm leading-tight',
    unread ? 'font-semibold text-foreground' : 'font-normal text-foreground/90'
  );

  const content: ReactNode = (
    <>
      {icon !== undefined && (
        <div className="flex-shrink-0" aria-hidden="true">
          {icon}
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex items-center gap-2">
          {unread && (
            <span
              data-testid="unread-dot"
              aria-hidden="true"
              className={clsx('inline-block h-2 w-2 flex-shrink-0 rounded-full', ENTITY_BG[entity])}
            />
          )}
          <span className={titleClasses}>{title}</span>
        </div>
        {body && (
          <p data-testid="notification-body" className="line-clamp-2 text-sm text-muted-foreground">
            {body}
          </p>
        )}
      </div>

      <div className="flex flex-shrink-0 flex-col items-end gap-2">
        <span
          className="whitespace-nowrap font-mono text-xs text-muted-foreground"
          style={{ fontFamily: 'var(--f-mono, ui-monospace, SFMono-Regular, Menlo, monospace)' }}
        >
          {timestamp}
        </span>
        {onDismiss && (
          <button
            type="button"
            aria-label={dismissAriaLabel}
            onClick={(e: MouseEvent<HTMLButtonElement>) => {
              e.stopPropagation();
              onDismiss();
            }}
            className="inline-flex h-6 w-6 items-center justify-center rounded-md text-muted-foreground opacity-0 transition-opacity hover:bg-muted hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100 md:opacity-100"
          >
            <DismissIcon />
          </button>
        )}
      </div>
    </>
  );

  if (onClick) {
    // When onDismiss is present, use a div with role=button to avoid nested-button DOM warnings.
    // When only onClick, use a real <button>.
    if (onDismiss) {
      const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
        // Only react when the key event originates on the outer div itself,
        // not on a descendant (e.g. the dismiss button). Prevents keyboard
        // Enter/Space on the dismiss button from bubbling up and firing onClick.
        if (e.target !== e.currentTarget) return;
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      };
      return (
        <div
          role="button"
          tabIndex={0}
          data-entity={entity}
          onClick={onClick}
          onKeyDown={handleKeyDown}
          className={containerClasses}
        >
          {content}
        </div>
      );
    }
    return (
      <button type="button" data-entity={entity} onClick={onClick} className={containerClasses}>
        {content}
      </button>
    );
  }

  return (
    <article data-entity={entity} className={containerClasses}>
      {content}
    </article>
  );
}
