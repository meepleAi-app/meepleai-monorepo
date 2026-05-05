/**
 * ErrorState — full-grid error surface for /shared-games.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 668-705.
 * 72px red circle (🛑), heading + description, primary "Riprova" button
 * filled with `hsl(var(--c-game))` brand color and game-tinted box-shadow.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface ErrorStateProps {
  readonly title: string;
  readonly description: string;
  readonly retryLabel: string;
  readonly onRetry: () => void;
  readonly className?: string;
}

export function ErrorState({
  title,
  description,
  retryLabel,
  onRetry,
  className,
}: ErrorStateProps): JSX.Element {
  return (
    <div
      data-slot="shared-games-error-state"
      role="alert"
      className={clsx(
        'col-span-full flex flex-col items-center justify-center gap-3 px-4 py-12 text-center',
        className
      )}
    >
      <div
        aria-hidden="true"
        className="flex h-[72px] w-[72px] items-center justify-center rounded-full bg-[hsl(var(--c-error)/0.12)] text-[32px]"
      >
        🛑
      </div>
      <h3 className="m-0 font-display text-[17px] font-bold leading-tight text-foreground">
        {title}
      </h3>
      <p className="m-0 max-w-[380px] text-[13px] leading-[1.55] text-[hsl(var(--text-sec))]">
        {description}
      </p>
      <button
        type="button"
        onClick={onRetry}
        className={clsx(
          'mt-2 inline-flex items-center justify-center rounded-full px-5 py-2 font-mono text-[12px] font-bold text-white',
          'bg-[hsl(var(--c-game))] shadow-[0_3px_10px_hsl(var(--c-game)/0.3)]',
          'transition-transform duration-150 hover:-translate-y-0.5',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2'
        )}
      >
        {retryLabel}
      </button>
    </div>
  );
}
