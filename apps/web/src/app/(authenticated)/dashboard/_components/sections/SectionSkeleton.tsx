/**
 * SectionSkeleton — shimmer placeholder for dashboard sections during loading.
 * Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx `SectionSkeleton`.
 *
 * Shape: same outer card as DashboardSection (rounded 18px, border, bg-card),
 * header shimmer (icon + title + view-all hint), body shimmer (min-h 80px).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface SectionSkeletonProps {
  /** Min-height of the body shimmer block, in pixels. */
  readonly bodyMinHeight?: number;
  /** When true, spans full width on desktop (matches Events row). */
  readonly fullWidth?: boolean;
  readonly className?: string;
}

export function SectionSkeleton({
  bodyMinHeight = 140,
  fullWidth,
  className,
}: SectionSkeletonProps): JSX.Element {
  return (
    <div
      data-slot="dashboard-section-skeleton"
      className={clsx(
        'flex min-h-[180px] flex-col gap-3 rounded-[18px] border border-border bg-card p-3.5 sm:p-[18px]',
        fullWidth && 'sm:col-span-2',
        className
      )}
      aria-hidden="true"
    >
      <div className="flex items-center gap-2 sm:gap-2.5">
        <div className="h-7 w-7 animate-pulse rounded-md bg-muted sm:h-8 sm:w-8" />
        <div className="h-3.5 w-2/5 animate-pulse rounded bg-muted" />
        <div className="flex-1" />
        <div className="h-2.5 w-16 animate-pulse rounded bg-muted" />
      </div>
      <div
        className="flex-1 animate-pulse rounded-[10px] bg-muted"
        style={{ minHeight: bodyMinHeight }}
      />
    </div>
  );
}
