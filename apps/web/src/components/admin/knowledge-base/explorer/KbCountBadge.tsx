'use client';

import type { JSX } from 'react';

interface KbCountBadgeProps {
  readonly count: number | undefined;
  readonly loading: boolean;
  readonly isError?: boolean;
  readonly tooltip?: string;
  readonly testId?: string;
}

export function KbCountBadge({
  count,
  loading,
  isError,
  tooltip,
  testId,
}: KbCountBadgeProps): JSX.Element {
  if (loading && count === undefined) {
    return (
      <span
        aria-hidden="true"
        data-testid={testId ? `${testId}-loading` : undefined}
        className="ml-1.5 inline-block h-4 w-6 rounded-full bg-muted animate-pulse"
      />
    );
  }

  if (isError && count === undefined) {
    return (
      <span
        aria-label="conteggio non disponibile"
        title={tooltip}
        data-testid={testId}
        className={[
          'ml-1.5 inline-flex items-center justify-center min-w-[1.5rem] h-4 px-1.5 rounded-full',
          'text-[10px] font-bold leading-none',
          'bg-muted text-muted-foreground',
        ].join(' ')}
      >
        —
      </span>
    );
  }

  const safe = count ?? 0;
  const display = safe > 99 ? '99+' : safe.toString();
  const isActive = safe > 0;

  return (
    <span
      aria-hidden="true"
      title={tooltip}
      data-testid={testId}
      className={[
        'ml-1.5 inline-flex items-center justify-center min-w-[1.5rem] h-4 px-1.5 rounded-full',
        'text-[10px] font-bold tabular-nums leading-none',
        isActive
          ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
          : 'bg-muted text-muted-foreground',
      ].join(' ')}
    >
      {display}
    </span>
  );
}
