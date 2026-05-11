/**
 * EmptyState — empty results surface for /shared-games.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 617-665.
 * Two kinds:
 *  - 'empty-search'   → 🔍 info-tinted circle + "Nessun gioco trovato"
 *  - 'filtered-empty' → ⚠️ warning-tinted circle + "Nessun match con i filtri attuali"
 * Outlined "↺ Reset filtri" button branded `hsl(var(--c-game))`.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export type EmptyStateKind = 'empty-search' | 'filtered-empty';

export interface EmptyStateProps {
  readonly kind: EmptyStateKind;
  readonly title: string;
  /** Optional description; spec uses `${query}` interpolation handled by caller. */
  readonly description?: string;
  readonly resetLabel: string;
  readonly onReset: () => void;
  readonly className?: string;
}

export function EmptyState({
  kind,
  title,
  description,
  resetLabel,
  onReset,
  className,
}: EmptyStateProps): JSX.Element {
  const isSearch = kind === 'empty-search';
  const tint = isSearch ? 'var(--c-info)' : 'var(--c-warning)';
  const icon = isSearch ? '🔍' : '⚠️';

  return (
    <div
      data-slot="shared-games-empty-state"
      data-kind={kind}
      role="status"
      aria-live="polite"
      className={clsx(
        'col-span-full flex flex-col items-center justify-center gap-3 px-4 py-12 text-center',
        className
      )}
    >
      <div
        aria-hidden="true"
        style={{ backgroundColor: `hsl(${tint} / 0.12)` }}
        className="flex h-[72px] w-[72px] items-center justify-center rounded-full text-[32px]"
      >
        {icon}
      </div>
      <h3 className="m-0 font-display text-[17px] font-bold leading-tight text-foreground">
        {title}
      </h3>
      {description ? (
        <p className="m-0 max-w-[380px] text-[13px] leading-[1.55] text-[hsl(var(--text-sec))]">
          {description}
        </p>
      ) : null}
      <button
        type="button"
        onClick={onReset}
        className={clsx(
          'mt-2 inline-flex items-center justify-center rounded-full px-5 py-2 font-mono text-[12px] font-bold',
          'border border-[hsl(var(--c-game)/0.5)] bg-transparent text-[hsl(var(--c-game))]',
          'transition-colors duration-150 hover:bg-[hsl(var(--c-game)/0.08)]',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2'
        )}
      >
        {resetLabel}
      </button>
    </div>
  );
}
