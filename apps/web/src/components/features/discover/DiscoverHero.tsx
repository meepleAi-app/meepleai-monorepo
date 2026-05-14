'use client';

import type { ReactNode } from 'react';

export interface DiscoverHeroProps {
  /** Localised h1 title. */
  readonly title: string;
  /** Localised subtitle / lead paragraph. */
  readonly subtitle?: string;
  /** SearchBox slot (rendered below title). */
  readonly searchSlot?: ReactNode;
  /** FilterPillBar slot (rendered below search). */
  readonly filterSlot?: ReactNode;
}

/**
 * DiscoverHero — gradient hero block per sp4-discover.jsx.
 *
 * Composes title + optional search + filter children inside a 3-color gradient
 * (entity event → toolkit → agent). Mockup-faithful pattern; not coupled to
 * DiscoverSearchBox or EntityFilterPillBar (both passed via slot props).
 */
export function DiscoverHero({ title, subtitle, searchSlot, filterSlot }: DiscoverHeroProps) {
  return (
    <header
      data-slot="discover-hero"
      className="relative overflow-hidden px-4 pt-6 pb-5 sm:px-8 sm:pt-10 sm:pb-7"
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--c-event) / 0.08) 0%, hsl(var(--c-toolkit) / 0.06) 50%, hsl(var(--c-agent) / 0.08) 100%)',
        borderBottom: '1px solid var(--border-light, rgba(180, 130, 80, 0.1))',
      }}
    >
      <div className="mx-auto flex max-w-5xl flex-col gap-4">
        <div className="flex flex-col gap-1">
          <span
            className="inline-flex w-fit items-center gap-1 rounded-full bg-muted/60 px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground"
            aria-hidden="true"
          >
            <span>🔍</span>Hub · /discover
          </span>
          <h1
            className="font-bold font-[Quicksand] text-2xl sm:text-3xl tracking-tight text-foreground"
            style={{ lineHeight: 1.1 }}
          >
            {title}
          </h1>
          {subtitle && (
            <p className="mt-1 max-w-xl text-sm text-muted-foreground" style={{ lineHeight: 1.55 }}>
              {subtitle}
            </p>
          )}
        </div>
        {searchSlot && <div data-slot="discover-hero-search">{searchSlot}</div>}
        {filterSlot && <div data-slot="discover-hero-filters">{filterSlot}</div>}
      </div>
    </header>
  );
}
