/**
 * ToolkitSummaryPanel — hero summary block for `/toolkits/[id]`.
 *
 * Wave 3 (#1145). Renders cover gradient + title + author chip + game chip +
 * inline stats (installCount, ratingAverage via Stars, currentVersion). Pure
 * presentational. Slots into the `hero` of `DetailPageLayout`.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { Stars } from './Stars';

export interface ToolkitSummaryPanelLabels {
  readonly authorChipPrefix: string;
  readonly gameChipPrefix: string;
  readonly statsHeading: string;
  readonly installCountLabel: string;
  readonly ratingLabel: string;
  readonly versionLabel: string;
  readonly noRatings: string;
}

export interface ToolkitSummaryPanelProps {
  readonly title: string;
  readonly description: string;
  readonly authorName: string;
  readonly authorAvatarUrl: string | null;
  readonly coverImageUrl: string | null;
  readonly gameName: string | null | undefined;
  readonly installCount: number;
  readonly ratingAverage: number | null;
  readonly ratingCount: number;
  readonly currentVersion: string;
  readonly labels: ToolkitSummaryPanelLabels;
  readonly className?: string;
}

export function ToolkitSummaryPanel({
  title,
  description,
  authorName,
  authorAvatarUrl,
  coverImageUrl,
  gameName,
  installCount,
  ratingAverage,
  ratingCount,
  currentVersion,
  labels,
  className,
}: ToolkitSummaryPanelProps): JSX.Element {
  return (
    <header
      data-slot="toolkit-detail-summary-panel"
      className={clsx(
        'relative overflow-hidden rounded-2xl border border-border bg-card p-5 sm:p-7',
        className
      )}
    >
      {/* Cover gradient backdrop */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 opacity-30"
        style={{
          background: coverImageUrl
            ? `url(${coverImageUrl}) center/cover`
            : 'linear-gradient(135deg, hsl(var(--c-toolkit) / 0.18) 0%, hsl(var(--c-game) / 0.14) 100%)',
        }}
      />

      <div className="relative flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <span
            data-slot="toolkit-detail-author-chip"
            className="inline-flex items-center gap-1.5 rounded-full bg-muted/70 px-2.5 py-1 text-xs font-bold font-[Quicksand] text-foreground"
          >
            {authorAvatarUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={authorAvatarUrl} alt="" className="h-4 w-4 rounded-full object-cover" />
            ) : (
              <span aria-hidden="true" className="text-sm">
                👤
              </span>
            )}
            <span>
              {labels.authorChipPrefix} {authorName}
            </span>
          </span>
          {gameName && (
            <span
              data-slot="toolkit-detail-game-chip"
              className="inline-flex items-center gap-1.5 rounded-full bg-muted/70 px-2.5 py-1 text-xs font-bold font-[Quicksand] text-foreground"
            >
              <span aria-hidden="true" className="text-sm">
                🎲
              </span>
              <span>
                {labels.gameChipPrefix} {gameName}
              </span>
            </span>
          )}
        </div>

        <h1 className="font-bold font-[Quicksand] text-2xl sm:text-3xl tracking-tight text-foreground">
          {title}
        </h1>

        {description && (
          <p className="max-w-2xl text-sm text-muted-foreground leading-relaxed line-clamp-3">
            {description}
          </p>
        )}

        <dl
          data-slot="toolkit-detail-summary-stats"
          className="mt-2 grid grid-cols-3 gap-4 sm:max-w-md"
          aria-label={labels.statsHeading}
        >
          <div>
            <dt className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              {labels.installCountLabel}
            </dt>
            <dd className="mt-1 font-bold font-[Quicksand] text-lg text-foreground tabular-nums">
              {installCount.toLocaleString()}
            </dd>
          </div>
          <div>
            <dt className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              {labels.ratingLabel}
            </dt>
            <dd className="mt-1">
              {ratingAverage != null ? (
                <Stars value={ratingAverage} showNumeric />
              ) : (
                <span className="font-mono text-xs text-muted-foreground">{labels.noRatings}</span>
              )}
              {ratingCount > 0 && (
                <span className="ml-1 font-mono text-[10px] text-muted-foreground">
                  ({ratingCount})
                </span>
              )}
            </dd>
          </div>
          <div>
            <dt className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              {labels.versionLabel}
            </dt>
            <dd className="mt-1 font-bold font-[Quicksand] text-lg text-foreground tabular-nums">
              {currentVersion}
            </dd>
          </div>
        </dl>
      </div>
    </header>
  );
}
