/**
 * RatingBreakdown — ratings summary + histogram + reviews for `/toolkits/[id]`.
 *
 * Wave 3 (#1479). Maps the mockup ReviewsTab (sp4-toolkit-detail.jsx:847-935).
 * Pure presentational, labels injected, dates pre-formatted by the caller.
 *
 * Source data: ToolkitRatingsResponse (averageStars, totalCount,
 * breakdown.star1..5, items: ToolkitRating[]). The v1 backend (Gate B) returns
 * a zero/empty stub until the ToolkitRating entity ships — hence the empty
 * state and the division-by-zero guard on the histogram bars.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { Stars } from './Stars';

export interface RatingBreakdownReview {
  readonly id: string;
  readonly raterDisplayName: string;
  readonly raterAvatarUrl: string | null;
  readonly stars: number;
  readonly comment: string | null;
  /** Pre-formatted, locale-aware timestamp supplied by the orchestrator. */
  readonly createdAtLabel: string;
}

export interface RatingBreakdownLabels {
  readonly reviewsCountLabel: string;
  readonly empty: string;
}

export interface RatingBreakdownBuckets {
  readonly star1: number;
  readonly star2: number;
  readonly star3: number;
  readonly star4: number;
  readonly star5: number;
}

export interface RatingBreakdownProps {
  readonly averageStars: number;
  readonly totalCount: number;
  readonly breakdown: RatingBreakdownBuckets;
  readonly reviews: readonly RatingBreakdownReview[];
  readonly labels: RatingBreakdownLabels;
  readonly className?: string;
}

const STAR_BUCKETS = [5, 4, 3, 2, 1] as const;

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function RatingBreakdown({
  averageStars,
  totalCount,
  breakdown,
  reviews,
  labels,
  className,
}: RatingBreakdownProps): JSX.Element {
  if (totalCount <= 0) {
    return (
      <div
        data-slot="toolkit-detail-rating-breakdown"
        className={clsx('rounded-lg border border-border bg-card p-6 text-center', className)}
      >
        <p className="font-mono text-xs text-muted-foreground">{labels.empty}</p>
      </div>
    );
  }

  return (
    <div
      data-slot="toolkit-detail-rating-breakdown"
      className={clsx('flex flex-col gap-4', className)}
    >
      <div
        data-slot="toolkit-detail-rating-summary"
        className="grid grid-cols-[auto_1fr] items-center gap-6 rounded-lg border border-border bg-card p-4"
      >
        <div className="text-center">
          <div className="font-bold font-[Quicksand] text-4xl leading-none text-foreground tabular-nums">
            {averageStars.toFixed(1)}
          </div>
          <div className="mt-1 flex justify-center">
            <Stars value={averageStars} />
          </div>
          <div className="mt-1 font-mono text-[10px] font-bold text-muted-foreground">
            {totalCount} {labels.reviewsCountLabel}
          </div>
        </div>
        <div className="flex flex-col gap-1.5">
          {STAR_BUCKETS.map(star => {
            const count = breakdown[`star${star}` as keyof RatingBreakdownBuckets];
            const pct = totalCount > 0 ? (count / totalCount) * 100 : 0;
            return (
              <div
                key={star}
                data-slot="toolkit-detail-rating-row"
                className="flex items-center gap-2"
              >
                <span className="w-5 font-mono text-[11px] font-bold text-muted-foreground">
                  {star}
                  <span aria-hidden="true">★</span>
                </span>
                <div className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
                  <div
                    data-slot="toolkit-detail-rating-bar"
                    className="h-full rounded-full bg-entity-agent"
                    style={{ width: `${pct}%` }}
                  />
                </div>
                <span className="w-7 text-right font-mono text-[10px] font-bold text-muted-foreground tabular-nums">
                  {count}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {reviews.length > 0 && (
        <ul data-slot="toolkit-detail-reviews" role="list" className="flex flex-col gap-2.5">
          {reviews.map(r => (
            <li
              key={r.id}
              data-slot="toolkit-detail-review"
              className="rounded-lg border border-border bg-card p-3.5"
            >
              <div className="mb-1.5 flex items-center gap-2.5">
                {r.raterAvatarUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={r.raterAvatarUrl}
                    alt=""
                    className="h-8 w-8 rounded-full object-cover"
                  />
                ) : (
                  <span
                    aria-hidden="true"
                    className="flex h-8 w-8 items-center justify-center rounded-full bg-muted font-bold font-[Quicksand] text-[11px] text-foreground"
                  >
                    {initialsOf(r.raterDisplayName)}
                  </span>
                )}
                <div className="min-w-0 flex-1">
                  <div className="font-bold font-[Quicksand] text-sm text-foreground">
                    {r.raterDisplayName}
                  </div>
                  <div className="flex items-center gap-2 font-mono text-[10px] text-muted-foreground">
                    <Stars value={r.stars} />
                    <span>{r.createdAtLabel}</span>
                  </div>
                </div>
              </div>
              {r.comment && (
                <p className="m-0 text-sm leading-relaxed text-foreground">{r.comment}</p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
