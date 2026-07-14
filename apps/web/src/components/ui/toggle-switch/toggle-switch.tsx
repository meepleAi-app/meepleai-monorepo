import type { JSX } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/entity-tokens';

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

  const trackClasses = clsx(
    'relative inline-flex items-center rounded-full transition-colors',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
    TRACK_CLASSES[size],
    checked ? 'bg-primary' : 'bg-muted',
    disabled && 'opacity-50 cursor-not-allowed',
    className
  );

  const thumbClasses = clsx(
    'inline-block rounded-full bg-white shadow transform transition-transform',
    THUMB_CLASSES[size],
    checked ? THUMB_TRANSLATE[size] : 'translate-x-0.5'
  );

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
      onClick={() => {
        if (!disabled) onCheckedChange(!checked);
      }}
    >
      <span className={thumbClasses} />
    </button>
  );
}
