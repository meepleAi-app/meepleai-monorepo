/**
 * HubToolkitsHero — top hero for `/toolkits` hub.
 *
 * Wave 4 (#1480). Maps the mockup HubToolkitsHero (sp4-hub-toolkits.jsx:233-280).
 * Gradient bg with entity-toolkit accent + eyebrow chip + h1 + subtitle + N stat
 * tiles. Pure presentational, labels and stats injected.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface HubToolkitsHeroStat {
  readonly label: string;
  readonly value: string | number;
  readonly unit?: string;
}

export interface HubToolkitsHeroLabels {
  readonly eyebrow: string;
  readonly title: string;
  readonly subtitle: string;
}

export interface HubToolkitsHeroProps {
  readonly stats: readonly HubToolkitsHeroStat[];
  readonly labels: HubToolkitsHeroLabels;
  readonly compact?: boolean;
  readonly className?: string;
}

export function HubToolkitsHero({
  stats,
  labels,
  compact = false,
  className,
}: HubToolkitsHeroProps): JSX.Element {
  return (
    <header
      data-slot="toolkits-index-hero"
      className={clsx(
        'border-b border-border bg-gradient-to-b from-entity-toolkit/10 to-transparent',
        compact ? 'px-4 pt-5 pb-4' : 'px-8 pt-8 pb-5',
        className
      )}
    >
      <div className="mb-2.5 flex items-center gap-1.5">
        <span className="inline-flex items-center gap-1 rounded-full bg-entity-toolkit/15 px-2 py-1 font-mono text-[9px] font-bold uppercase tracking-wider text-entity-toolkit">
          <span aria-hidden="true">🧰</span>
          {labels.eyebrow}
        </span>
      </div>
      <h1
        className={clsx(
          'm-0 mb-1.5 font-bold font-[Quicksand] tracking-tight text-foreground leading-tight',
          compact ? 'text-2xl' : 'text-3xl sm:text-4xl'
        )}
      >
        {labels.title}
      </h1>
      <p
        className={clsx(
          'mb-3.5 max-w-xl text-muted-foreground leading-relaxed',
          compact ? 'text-[13px]' : 'text-sm'
        )}
      >
        {labels.subtitle}
      </p>
      <dl
        className={clsx(
          'm-0 flex flex-wrap gap-x-5 gap-y-2 font-mono',
          compact ? 'text-[11px]' : 'text-xs'
        )}
      >
        {stats.map(s => (
          <div key={s.label} data-slot="toolkits-index-hero-stat" className="flex flex-col gap-0.5">
            <dt className="text-[9px] font-bold uppercase tracking-wider text-muted-foreground">
              {s.label}
            </dt>
            <dd
              className={clsx(
                'm-0 flex items-baseline gap-1 font-bold font-[Quicksand] leading-none text-foreground tabular-nums',
                compact ? 'text-lg' : 'text-xl'
              )}
            >
              <span>{s.value}</span>
              {s.unit && (
                <span className="text-[11px] font-semibold text-muted-foreground">{s.unit}</span>
              )}
            </dd>
          </div>
        ))}
      </dl>
    </header>
  );
}
