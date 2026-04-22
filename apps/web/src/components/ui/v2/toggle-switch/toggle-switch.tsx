import type { CSSProperties, JSX } from 'react';

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

export type ToggleSwitchSize = 'sm' | 'md';

export interface ToggleSwitchProps {
  readonly checked: boolean;
  readonly onCheckedChange: (next: boolean) => void;
  readonly disabled?: boolean;
  readonly entity?: EntityType;
  readonly size?: ToggleSwitchSize;
  readonly ariaLabel?: string;
  readonly ariaLabelledBy?: string;
  readonly className?: string;
  readonly id?: string;
}

const TRACK_CLASSES: Record<ToggleSwitchSize, string> = {
  sm: 'w-8 h-5',
  md: 'w-10 h-6',
};

const THUMB_CLASSES: Record<ToggleSwitchSize, string> = {
  sm: 'h-4 w-4',
  md: 'h-5 w-5',
};

const THUMB_TRANSLATE: Record<ToggleSwitchSize, string> = {
  sm: 'translate-x-3',
  md: 'translate-x-4',
};

export function ToggleSwitch({
  checked,
  onCheckedChange,
  disabled = false,
  entity = 'game',
  size = 'md',
  ariaLabel,
  ariaLabelledBy,
  className,
  id,
}: ToggleSwitchProps): JSX.Element {
  if (!ariaLabel && !ariaLabelledBy && process.env.NODE_ENV !== 'production') {
    console.warn('ToggleSwitch: provide `ariaLabel` or `ariaLabelledBy` for accessibility.');
  }

  const cssKey = ENTITY_CSS_VAR_KEY[entity];
  const entityColor = `hsl(var(--e-${cssKey}))`;

  const trackStyle: CSSProperties = checked ? { backgroundColor: entityColor } : {};

  const trackClasses = clsx(
    'relative inline-flex items-center rounded-full transition-colors',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
    TRACK_CLASSES[size],
    !checked && 'bg-muted',
    disabled && 'opacity-50 cursor-not-allowed',
    className
  );

  const thumbClasses = clsx(
    'inline-block rounded-full bg-white shadow transform transition-transform',
    THUMB_CLASSES[size],
    checked ? THUMB_TRANSLATE[size] : 'translate-x-0.5'
  );

  const focusRingStyle: CSSProperties = {
    // via CSS custom property so ring uses entity color
    ['--tw-ring-color' as string]: entityColor,
  };

  return (
    <button
      type="button"
      role="switch"
      id={id}
      aria-checked={checked}
      aria-label={ariaLabel}
      aria-labelledby={ariaLabelledBy}
      aria-disabled={disabled || undefined}
      disabled={disabled}
      data-entity={entity}
      className={trackClasses}
      style={{ ...trackStyle, ...focusRingStyle }}
      onClick={() => {
        if (!disabled) onCheckedChange(!checked);
      }}
    >
      <span className={thumbClasses} />
    </button>
  );
}
