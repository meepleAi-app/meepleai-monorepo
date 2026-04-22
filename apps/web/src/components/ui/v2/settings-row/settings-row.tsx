import type { CSSProperties, JSX, ReactNode } from 'react';

import clsx from 'clsx';

import type { EntityType } from '../entity-tokens';

// Mirror entity-card / btn ENTITY_CSS_VAR_KEY mapping so `kb` resolves to `--e-document`
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
}: SettingsRowProps): JSX.Element {
  const isInteractive = Boolean(onClick || href);
  // onClick wins when both provided
  const useButton = Boolean(onClick);
  const useLink = !useButton && Boolean(href);

  const iconStyle: CSSProperties | undefined = entity
    ? { color: `hsl(var(--e-${ENTITY_CSS_VAR_KEY[entity]}))` }
    : undefined;

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
        <span data-testid="settings-row-icon" className="flex-shrink-0 text-lg" style={iconStyle}>
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
    <li className={className}>
      {useButton ? (
        <button
          type="button"
          className={innerClasses}
          onClick={disabled ? undefined : onClick}
          disabled={disabled}
          aria-disabled={disabled || undefined}
        >
          {body}
        </button>
      ) : useLink ? (
        <a
          href={disabled ? undefined : href}
          className={innerClasses}
          aria-disabled={disabled || undefined}
        >
          {body}
        </a>
      ) : (
        <div className={innerClasses}>{body}</div>
      )}
    </li>
  );
}
