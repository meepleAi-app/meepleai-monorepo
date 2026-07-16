import type { JSX, MouseEventHandler, ReactNode } from 'react';

import { Slot } from '@radix-ui/react-slot';
import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

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

/**
 * Per-entity solid background for the primary variant (issue #2955, Fase 1).
 * Literal class strings so Tailwind's content scanner emits the utilities — a
 * dynamic `bg-entity-${entity}` would not be generated. `kb` maps to the
 * registered `-kb` (teal) token, NOT `-document` (slate), which lives only in
 * `@layer tokens` and is absent from `@theme inline`, so it would not render.
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

/**
 * Hover shade for the entity primary variant (issue #2955, Fase 1). Dims the
 * entity's OWN hue to 90% opacity, preserving the hover affordance without
 * reverting to theme `bg-primary/90` (which would flip e.g. a purple `player`
 * button to orange). Literal strings for Tailwind's content scanner.
 */
const ENTITY_BG_HOVER: Record<EntityType, string> = {
  game: 'hover:bg-entity-game/90',
  player: 'hover:bg-entity-player/90',
  session: 'hover:bg-entity-session/90',
  agent: 'hover:bg-entity-agent/90',
  kb: 'hover:bg-entity-kb/90',
  chat: 'hover:bg-entity-chat/90',
  event: 'hover:bg-entity-event/90',
  toolkit: 'hover:bg-entity-toolkit/90',
  tool: 'hover:bg-entity-tool/90',
};

/**
 * Per-entity border for the outline variant (issue #2955, Fase 2). Literal
 * class strings for Tailwind's content scanner. `kb` maps to the registered
 * `-kb` (teal) token, NOT `-document` (slate).
 */
const ENTITY_BORDER: Record<EntityType, string> = {
  game: 'border-entity-game',
  player: 'border-entity-player',
  session: 'border-entity-session',
  agent: 'border-entity-agent',
  kb: 'border-entity-kb',
  chat: 'border-entity-chat',
  event: 'border-entity-event',
  toolkit: 'border-entity-toolkit',
  tool: 'border-entity-tool',
};

/**
 * Per-entity AA text-on-tint label for the outline variant (issue #2955, Fase
 * 2). The `-text` variant (verified >=4.5:1 in Fase 0) keeps the outline label
 * readable on the transparent surface and the blocking axe AA gate green.
 * Literal strings for Tailwind's content scanner; `kb` -> registered `-kb-text`.
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

  // Fase 1 (#2955): the primary variant carries the per-entity background when an
  // `entity` is supplied; the label stays on `text-primary-foreground`.
  // Fase 2 (#2955): the outline variant carries the per-entity border + AA
  // text-on-tint label. Both fall back to the flat token when no entity is set,
  // and the other variants (secondary/ghost/destructive) are unaffected.
  const variantClasses =
    variant === 'primary' && entity
      ? clsx('text-primary-foreground', ENTITY_BG[entity], ENTITY_BG_HOVER[entity])
      : variant === 'outline' && entity
        ? clsx('border bg-transparent hover:bg-muted', ENTITY_BORDER[entity], ENTITY_TEXT[entity])
        : VARIANT_CLASSES[variant];

  const classes = clsx(
    'inline-flex items-center justify-center gap-2 font-semibold rounded-xl transition-colors',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
    'disabled:opacity-50 disabled:pointer-events-none',
    SIZE_CLASSES[size],
    variantClasses,
    fullWidth && 'w-full',
    className
  );

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
        data-entity={entity || undefined}
        className={classes}
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
      data-entity={entity || undefined}
      className={classes}
      disabled={isDisabled}
      aria-busy={loading || undefined}
      onClick={onClick}
    >
      {content}
    </button>
  );
}
