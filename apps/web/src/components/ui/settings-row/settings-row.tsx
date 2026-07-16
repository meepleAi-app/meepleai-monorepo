import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

/**
 * Per-entity AA text-on-tint shade for the leading icon (issue #2955, Fase 2).
 * Literal class strings so Tailwind's content scanner emits the utilities — a
 * dynamic `text-entity-${entity}-text` would not be generated. The `-text`
 * variant (verified >=4.5:1 in Fase 0) keeps the blocking axe AA gate green.
 * `kb` maps to the registered `-kb-text` (teal), NOT `-document` (slate), which
 * lives only in `@layer tokens` and would not render.
 */
const ENTITY_TEXT: Record<EntityType, string> = {
  game: 'text-entity-game-text',
  player: 'text-entity-player-text',
  session: 'text-entity-session-text',
  agent: 'text-entity-agent-text',
  kb: 'text-entity-kb-text',
  chat: 'text-entity-chat-text',
  event: 'text-entity-event-text',
  toolkit: 'text-entity-toolkit-text',
  tool: 'text-entity-tool-text',
};

export interface SettingsRowProps {
  readonly icon?: ReactNode;
  readonly label: string;
  readonly description?: string;
  readonly trailing?: ReactNode;
  readonly onClick?: () => void;
  readonly href?: string;
  readonly destructive?: boolean;
  readonly entity?: EntityType;
  readonly disabled?: boolean;
  readonly className?: string;
  readonly 'aria-current'?: 'true' | 'false' | 'page' | 'step' | 'location' | 'date' | 'time';
}

function ChevronRight(): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      className="h-4 w-4 text-muted-foreground flex-shrink-0"
      viewBox="0 0 20 20"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path
        d="M7.5 5L12.5 10L7.5 15"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function SettingsRow({
  icon,
  label,
  description,
  trailing,
  onClick,
  href,
  destructive = false,
  entity,
  disabled = false,
  className,
  'aria-current': ariaCurrent,
}: SettingsRowProps): JSX.Element {
  const isInteractive = Boolean(onClick || href);
  // onClick wins when both provided
  const useButton = Boolean(onClick);
  const useLink = !useButton && Boolean(href);

  const showAutoChevron = isInteractive && trailing === undefined;

  const innerClasses = clsx(
    'flex items-center gap-3 w-full p-4 text-left',
    isInteractive && !disabled && 'hover:bg-muted/40 transition-colors',
    disabled && 'opacity-50 cursor-not-allowed'
  );

  const labelClasses = clsx(
    'text-sm font-medium truncate',
    destructive ? 'text-[hsl(var(--destructive))]' : 'text-foreground'
  );

  const body = (
    <>
      {icon !== undefined ? (
        <span
          data-testid="settings-row-icon"
          className={clsx('flex-shrink-0 text-lg', entity && ENTITY_TEXT[entity])}
        >
          {icon}
        </span>
      ) : null}
      <span className="flex-1 min-w-0 flex flex-col">
        <span className={labelClasses}>{label}</span>
        {description ? (
          <span className="text-xs text-muted-foreground truncate">{description}</span>
        ) : null}
      </span>
      {trailing !== undefined ? (
        <span data-testid="settings-row-trailing" className="flex-shrink-0">
          {trailing}
        </span>
      ) : null}
      {showAutoChevron ? <ChevronRight /> : null}
    </>
  );

  return (
    <li className={className} data-entity={entity}>
      {useButton ? (
        <button
          type="button"
          className={innerClasses}
          onClick={disabled ? undefined : onClick}
          disabled={disabled}
          aria-disabled={disabled || undefined}
          aria-current={ariaCurrent}
        >
          {body}
        </button>
      ) : useLink ? (
        disabled ? (
          // When the row is disabled, render a non-interactive <span> so that
          // we never emit an <a> without an href (jsx-a11y/anchor-is-valid).
          <span className={innerClasses} aria-disabled="true" aria-current={ariaCurrent}>
            {body}
          </span>
        ) : (
          <a href={href} className={innerClasses} aria-current={ariaCurrent}>
            {body}
          </a>
        )
      ) : (
        <div className={innerClasses} aria-current={ariaCurrent}>
          {body}
        </div>
      )}
    </li>
  );
}
