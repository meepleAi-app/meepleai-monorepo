/**
 * HubToolkitCardGrid — single toolkit card for the `/toolkits` hub grid.
 *
 * Wave 4 (#1480). Maps the mockup HubToolkitCardGrid (sp4-hub-toolkits.jsx:104-228).
 * Despite the mockup name, this is the per-item card, NOT a grid container —
 * the orchestrator renders many of these inside a CSS grid.
 *
 * Props derive verbatim from `RecommendedToolkit` (BE Zod schema). Several
 * mockup fields are P83 deferred (no BE backing yet — version / toolCount /
 * useCount / gameName / badge); the component gracefully hides them when
 * undefined so wiring to richer BE later requires no component refactor.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { Stars } from '@/components/ui/feedback/Stars';

export interface HubToolkitCardItem {
  readonly id: string;
  readonly title: string;
  readonly authorName: string;
  readonly installCount: number;
  readonly ratingAverage: number | null;
  readonly ratingCount: number;
  readonly coverImageUrl: string | null;
  /** FE-side emoji fallback when coverImageUrl is null. */
  readonly coverEmoji?: string;
  // P83 deferred fields (no BE backing yet — graceful hide when undefined)
  readonly version?: number;
  readonly toolCount?: number;
  readonly useCount?: number;
  readonly gameName?: string | null;
  readonly badge?: string;
}

export interface HubToolkitCardGridLabels {
  readonly gameRefFallback: string;
  readonly installCta: string;
  /** Template with `{title}` placeholder, e.g. `"Installa {title}"`. */
  readonly installAriaLabel: string;
  readonly toolsLabel: string;
  readonly usesLabel: string;
}

export interface HubToolkitCardGridProps {
  readonly toolkit: HubToolkitCardItem;
  readonly onInstall?: (id: string) => void;
  readonly onClick?: (id: string) => void;
  readonly labels: HubToolkitCardGridLabels;
  readonly compact?: boolean;
  readonly className?: string;
}

function interpolate(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, k: string) => String(vars[k] ?? ''));
}

export function HubToolkitCardGrid({
  toolkit,
  onInstall,
  onClick,
  labels,
  compact = false,
  className,
}: HubToolkitCardGridProps): JSX.Element {
  const installLabel = interpolate(labels.installAriaLabel, { title: toolkit.title });
  return (
    <article
      data-slot="toolkits-index-card"
      tabIndex={0}
      onClick={() => onClick?.(toolkit.id)}
      className={clsx(
        'group relative flex cursor-pointer flex-col overflow-hidden rounded-lg border border-border bg-card',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-toolkit/40',
        className
      )}
    >
      {/* Cover */}
      <div
        className={clsx(
          'relative flex items-center justify-center overflow-hidden bg-muted',
          compact ? 'aspect-[4/3]' : 'aspect-[5/3]'
        )}
      >
        {toolkit.coverImageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={toolkit.coverImageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <span
            aria-hidden="true"
            className={clsx('drop-shadow-lg', compact ? 'text-5xl' : 'text-6xl')}
          >
            {toolkit.coverEmoji ?? '🧰'}
          </span>
        )}
        {toolkit.badge && (
          // Decorative overlay on a variable cover image — must stay dark in both
          // light and dark themes for legibility; no semantic DS-15 token maps to
          // "always dark", so the literal colors are intentional here.
          // eslint-disable-next-line local/no-hardcoded-color-utility
          <span className="absolute top-2 left-2 rounded-full bg-black/50 px-2 py-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-white backdrop-blur-sm">
            {toolkit.badge}
          </span>
        )}
        <span className="absolute top-2 right-2 inline-flex items-center gap-1 rounded-full bg-entity-toolkit/95 px-1.5 py-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-white">
          <span aria-hidden="true">🧰</span>Toolkit
        </span>
        <div className="pointer-events-none absolute inset-x-0 bottom-0 h-2/5 bg-gradient-to-b from-transparent to-black/35" />
        {/*
          Decorative install-count pill on cover image — must stay light in both
          themes for legibility against the dark gradient overlay; no semantic
          DS-15 token maps to "always light".
        */}
        {/* eslint-disable-next-line local/no-hardcoded-color-utility */}
        <div className="absolute bottom-2 right-2 inline-flex items-center gap-1 rounded-full bg-white/90 px-2 py-0.5 font-mono text-[10px] font-bold text-foreground">
          <span aria-hidden="true">⬇</span>
          <span className="tabular-nums">{toolkit.installCount}</span>
        </div>
      </div>

      {/* Body */}
      <div className={clsx('flex flex-1 flex-col', compact ? 'gap-1.5 p-2.5' : 'gap-2 p-3.5')}>
        <div>
          <h2
            className={clsx(
              'm-0 mb-0.5 line-clamp-1 font-bold font-[Quicksand] text-foreground leading-tight',
              compact ? 'text-[13px]' : 'text-[15px]'
            )}
          >
            {toolkit.title}
          </h2>
          <div className="font-mono text-[10px] font-semibold text-muted-foreground">
            {toolkit.authorName}
          </div>
          {toolkit.ratingAverage !== null && (
            <div
              data-slot="toolkits-index-card-rating"
              className="mt-1 flex items-center gap-1.5 font-mono text-[10px] font-semibold text-muted-foreground"
            >
              <Stars value={Math.round(toolkit.ratingAverage)} />
              <span className="tabular-nums">{toolkit.ratingAverage.toFixed(1)}</span>
              {toolkit.version !== undefined && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>v{toolkit.version}.0</span>
                </>
              )}
            </div>
          )}
        </div>
        {(toolkit.toolCount !== undefined || toolkit.useCount !== undefined) && (
          <div className="font-mono text-[10px] font-semibold text-foreground">
            {toolkit.toolCount !== undefined && (
              <>
                <span className="text-muted-foreground">{labels.toolsLabel}</span>{' '}
                {toolkit.toolCount}
              </>
            )}
            {toolkit.toolCount !== undefined && toolkit.useCount !== undefined && ' · '}
            {toolkit.useCount !== undefined && (
              <>
                <span className="text-muted-foreground">{labels.usesLabel}</span> {toolkit.useCount}
              </>
            )}
          </div>
        )}
        {toolkit.gameName ? (
          <div className="inline-flex items-center gap-1 font-mono text-[10px] font-bold text-entity-game">
            <span aria-hidden="true">🎲</span>
            <span className="truncate">{toolkit.gameName}</span>
          </div>
        ) : (
          <div className="font-mono text-[10px] font-bold italic text-muted-foreground">
            {labels.gameRefFallback}
          </div>
        )}
      </div>

      {/* Hover install button */}
      <button
        type="button"
        aria-label={installLabel}
        onClick={e => {
          e.stopPropagation();
          onInstall?.(toolkit.id);
        }}
        className={clsx(
          'absolute left-3.5 right-3.5 translate-y-2 rounded-md bg-entity-toolkit px-3 py-2 font-bold font-[Quicksand] text-xs text-white opacity-0 shadow-lg transition-all',
          'group-hover:translate-y-0 group-hover:opacity-100 focus-visible:translate-y-0 focus-visible:opacity-100',
          compact ? 'bottom-2.5' : 'bottom-3.5'
        )}
      >
        {labels.installCta}
      </button>
    </article>
  );
}
