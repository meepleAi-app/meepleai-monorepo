/**
 * MeepleCardGameSkeleton — placeholder for the shared-games grid while loading.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596). Visual height matches
 * the `grid` MeepleCard variant (378px) so the grid doesn't reflow on hydrate.
 */
import type { JSX } from 'react';

import clsx from 'clsx';

export interface MeepleCardGameSkeletonProps {
  readonly className?: string;
}

export function MeepleCardGameSkeleton({ className }: MeepleCardGameSkeletonProps): JSX.Element {
  return (
    <div
      role="status"
      aria-busy="true"
      aria-label="Caricamento"
      className={clsx('animate-pulse rounded-2xl bg-[var(--mc-bg-muted)] h-[378px]', className)}
    />
  );
}
