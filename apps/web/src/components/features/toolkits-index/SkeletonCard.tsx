/**
 * SkeletonCard — loading placeholder per card in `/toolkits` hub.
 *
 * Wave 4 (#1480). Maps the mockup SkeletonCard (sp4-hub-toolkits.jsx:454-471).
 * Used by the orchestrator while `useDiscoverRecommendedToolkits` is loading.
 *
 * Purely decorative — `aria-hidden="true"` so the skeleton is invisible to AT
 * (the orchestrator handles `aria-busy`/`aria-live` at grid level).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface SkeletonCardProps {
  readonly compact?: boolean;
  readonly className?: string;
}

export function SkeletonCard({ compact = false, className }: SkeletonCardProps): JSX.Element {
  return (
    <div
      data-slot="toolkits-index-skeleton-card"
      aria-hidden="true"
      className={clsx('overflow-hidden rounded-lg border border-border bg-card', className)}
    >
      <div
        data-slot="toolkits-index-skeleton-cover"
        className={clsx('animate-pulse bg-muted', compact ? 'aspect-[4/3]' : 'aspect-[5/3]')}
      />
      <div className={clsx('flex flex-col gap-1.5', compact ? 'p-2.5' : 'p-3.5')}>
        <div
          data-slot="toolkits-index-skeleton-line"
          className="h-3.5 w-[72%] animate-pulse rounded bg-muted"
        />
        <div
          data-slot="toolkits-index-skeleton-line"
          className="h-2.5 w-[52%] animate-pulse rounded bg-muted"
        />
        <div
          data-slot="toolkits-index-skeleton-line"
          className="h-2.5 w-[40%] animate-pulse rounded bg-muted"
        />
      </div>
    </div>
  );
}
