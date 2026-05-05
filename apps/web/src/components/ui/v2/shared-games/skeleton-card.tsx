/**
 * SkeletonCard — loading placeholder for MeepleCardGame.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 277-295.
 * Uses `mai-shimmer` global keyframe (FAQ pattern, see `apps/web/src/styles/*`).
 * Honors `motion-reduce:animate-none` per a11y reduced-motion guard.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface SkeletonCardProps {
  /** Reduces cover height (96 vs 116) for compact layouts. */
  readonly compact?: boolean;
  readonly className?: string;
}

export function SkeletonCard({ compact = false, className }: SkeletonCardProps): JSX.Element {
  const coverH = compact ? 'h-24' : 'h-[116px]';
  return (
    <div
      data-slot="shared-games-skeleton-card"
      aria-hidden="true"
      className={clsx(
        'flex flex-col gap-1.5 overflow-hidden rounded-lg border border-border bg-card p-0',
        className
      )}
    >
      <div
        className={clsx(coverH, 'mai-shimmer bg-[hsl(var(--bg-muted))] motion-reduce:animate-none')}
      />
      <div className="flex flex-col gap-1.5 px-3 pb-3 pt-2">
        <div className="mai-shimmer h-[13px] w-3/4 rounded bg-[hsl(var(--bg-muted))] motion-reduce:animate-none" />
        <div className="mai-shimmer h-[10px] w-1/2 rounded bg-[hsl(var(--bg-muted))] motion-reduce:animate-none" />
        <div className="mai-shimmer mt-1 h-4 w-2/3 rounded-full bg-[hsl(var(--bg-muted))] motion-reduce:animate-none" />
      </div>
    </div>
  );
}
