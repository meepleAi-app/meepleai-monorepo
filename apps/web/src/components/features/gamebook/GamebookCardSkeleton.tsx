/**
 * GamebookCardSkeleton — SP6 Phase B Task 2 v2 component (Issue #788).
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-index.jsx`
 * (SkeletonCard).
 *
 * Pure component: no i18n, decorative-only (`aria-hidden="true"`).
 *
 * Layout: matches `GamebookCard` dimensions (16:9 cover + body padding) so
 * the page does NOT shift when real cards swap in. Renders shimmer
 * placeholders for cover, title, meta, and a status pill.
 *
 * Reduced motion: shimmer animation is gated by Tailwind's `motion-safe:`
 * variant. Under `prefers-reduced-motion: reduce`, the shimmer collapses
 * to a static `bg-muted` (no animation).
 *
 * data-slot="gamebook-card-skeleton" for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface GamebookCardSkeletonProps {
  readonly className?: string;
}

/**
 * Shimmer block — base utility for placeholders.
 *
 * `motion-safe:animate-pulse` runs only when prefers-reduced-motion is NOT
 * set to `reduce`. Under reduce, only the static `bg-muted` is visible —
 * no animation (Tailwind handles the gating natively).
 */
function ShimmerBlock({ className }: { className?: string }): ReactElement {
  return (
    <div
      aria-hidden="true"
      className={clsx('rounded-md bg-muted motion-safe:animate-pulse', className)}
    />
  );
}

export function GamebookCardSkeleton({ className }: GamebookCardSkeletonProps): ReactElement {
  return (
    <div
      data-slot="gamebook-card-skeleton"
      aria-hidden="true"
      className={clsx(
        'flex flex-col overflow-hidden rounded-xl border border-border bg-card',
        className
      )}
    >
      {/* Cover placeholder (16:9 aspect to match GamebookCard) */}
      <ShimmerBlock className="aspect-[16/9] w-full rounded-none" />

      {/* Body placeholders */}
      <div className="flex flex-col gap-3 p-3 sm:p-4">
        <ShimmerBlock className="h-4 w-4/5" />
        <ShimmerBlock className="h-3 w-1/2" />
        <ShimmerBlock className="h-5 w-24 rounded-full" />
      </div>
    </div>
  );
}
