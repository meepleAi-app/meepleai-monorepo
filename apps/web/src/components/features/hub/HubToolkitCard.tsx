/**
 * HubToolkitCard — authenticated catalog card for `/hub/toolkits` (#1166).
 * Cover gradient + entity chip + name + version + tools/uses count + hover install button.
 */
'use client';

import Link from 'next/link';

import type { RecommendedToolkit } from '@/lib/api/schemas/discover-cross-cutting.schemas';

export interface HubToolkitCardLabels {
  readonly installLabel: string;
  readonly toolsTemplate: string;
  readonly installsTemplate: string;
}

export interface HubToolkitCardProps {
  readonly toolkit: RecommendedToolkit;
  readonly labels: HubToolkitCardLabels;
  readonly onClick?: (id: string) => void;
  readonly onInstall?: (id: string) => void;
}

export function HubToolkitCard({ toolkit, labels, onClick, onInstall }: HubToolkitCardProps) {
  return (
    <Link
      href={`/toolkits/${toolkit.id}`}
      data-slot="hub-toolkit-card"
      onClick={() => onClick?.(toolkit.id)}
      className="group relative flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-foreground/25"
    >
      <div
        aria-hidden="true"
        className="flex aspect-[5/3] items-center justify-center bg-gradient-to-br from-[hsl(var(--c-toolkit)/0.22)] to-[hsl(var(--c-toolkit)/0.05)] text-4xl"
      >
        {toolkit.coverImageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={toolkit.coverImageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          '🧰'
        )}
        <span className="absolute right-2 top-2 inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-toolkit)/0.95)] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider text-background">
          🧰 Toolkit
        </span>
        {toolkit.ratingAverage != null && toolkit.ratingCount > 0 && (
          <span className="absolute bottom-2 right-2 inline-flex items-center gap-1 rounded-full bg-card/90 px-2 py-0.5 font-mono text-[10px] font-extrabold text-foreground backdrop-blur">
            ★ {toolkit.ratingAverage.toFixed(1)}
          </span>
        )}
      </div>
      <div className="flex flex-col gap-1 p-3">
        <h3 className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
          {toolkit.name}
        </h3>
        <div className="line-clamp-1 font-mono text-[10px] text-muted-foreground">
          {toolkit.authorName} ·{' '}
          {labels.installsTemplate.replace('{count}', String(toolkit.installCount))}
        </div>
      </div>
      <button
        type="button"
        data-slot="hub-card-install-button"
        onClick={event => {
          event.preventDefault();
          event.stopPropagation();
          onInstall?.(toolkit.id);
        }}
        className="absolute bottom-3 left-3 right-3 translate-y-2 rounded-lg bg-foreground py-1.5 font-bold font-[Quicksand] text-xs text-background opacity-0 transition-all group-hover:translate-y-0 group-hover:opacity-100 focus-visible:translate-y-0 focus-visible:opacity-100"
      >
        + {labels.installLabel}
      </button>
    </Link>
  );
}
