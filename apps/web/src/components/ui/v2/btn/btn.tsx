import type { CSSProperties, JSX, MouseEventHandler, ReactNode } from 'react';

import { Slot } from '@radix-ui/react-slot';
import clsx from 'clsx';

import type { EntityType } from '../entity-tokens';

// Map EntityType -> CSS variable key. Mirrors entity-card mapping
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

export type BtnVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'destructive';
export type BtnSize = 'sm' | 'md' | 'lg';

export interface BtnProps {
  readonly variant?: BtnVariant;
  readonly size?: BtnSize;
  readonly entity?: EntityType;
  readonly loading?: boolean;
  readonly fullWidth?: boolean;
  readonly leftIcon?: ReactNode;
  readonly rightIcon?: ReactNode;
  readonly asChild?: boolean;
  readonly className?: string;
  readonly children?: ReactNode;
  readonly type?: 'button' | 'submit' | 'reset';
  readonly disabled?: boolean;
  readonly onClick?: MouseEventHandler<HTMLButtonElement>;
  readonly id?: string;
  readonly 'data-testid'?: string;
}

const SIZE_CLASSES: Record<BtnSize, string> = {
  sm: 'h-8 px-3 text-xs',
  md: 'h-10 px-4 text-sm',
  lg: 'h-12 px-6 text-base',
};

const VARIANT_CLASSES: Record<BtnVariant, string> = {
  primary: 'bg-primary text-primary-foreground hover:bg-primary/90',
  secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
  outline: 'border border-border bg-transparent hover:bg-muted',
  ghost: 'bg-transparent hover:bg-muted',
  destructive: 'bg-destructive text-destructive-foreground hover:bg-destructive/90',
};

function Spinner(): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      className="animate-spin h-4 w-4"
      viewBox="0 0 24 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeOpacity="0.25" strokeWidth="4" />
      <path
        d="M22 12a10 10 0 0 1-10 10"
        stroke="currentColor"
        strokeWidth="4"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function Btn({
  variant = 'primary',
  size = 'md',
  entity,
  loading = false,
  fullWidth = false,
  leftIcon,
  rightIcon,
  asChild = false,
  className,
  children,
  type = 'button',
  disabled = false,
  onClick,
  id,
  'data-testid': testId,
}: BtnProps): JSX.Element {
  const isDisabled = disabled || loading;

  const classes = clsx(
    'inline-flex items-center justify-center gap-2 font-semibold rounded-xl transition-colors',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
    'disabled:opacity-50 disabled:pointer-events-none',
    SIZE_CLASSES[size],
    VARIANT_CLASSES[variant],
    fullWidth && 'w-full',
    className
  );

  const style: CSSProperties | undefined = (() => {
    if (!entity) return undefined;
    const key = ENTITY_CSS_VAR_KEY[entity];
    const color = `hsl(var(--e-${key}))`;
    if (variant === 'primary') {
      return { backgroundColor: color, color: 'white' };
    }
    if (variant === 'outline') {
      return { borderColor: color, color };
    }
    return undefined;
  })();

  const content = (
    <>
      {loading ? <Spinner /> : leftIcon}
      {children}
      {rightIcon}
    </>
  );

  if (asChild) {
    return (
      <Slot
        id={id}
        data-testid={testId}
        className={classes}
        style={style}
        data-loading={loading || undefined}
        aria-busy={loading || undefined}
      >
        {children as JSX.Element}
      </Slot>
    );
  }

  return (
    <button
      type={type}
      id={id}
      data-testid={testId}
      className={classes}
      style={style}
      disabled={isDisabled}
      aria-busy={loading || undefined}
      onClick={onClick}
    >
      {content}
    </button>
  );
}
