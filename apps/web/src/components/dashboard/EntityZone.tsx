'use client';

import { useId, type ReactNode } from 'react';

import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface EntityZoneProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  viewAllHref?: string;
  children: ReactNode;
}

export function EntityZone({ entity, title, count, viewAllHref, children }: EntityZoneProps) {
  const titleId = useId();

  return (
    <section className={`e-${entity} space-y-3`} aria-labelledby={titleId}>
      <div className="flex items-center gap-2.5 pb-3">
        <span
          data-testid="entity-badge"
          role="presentation"
          className="inline-flex items-center gap-1 rounded-md bg-[hsl(var(--e)/0.12)] px-2 py-0.5 font-quicksand text-[11px] font-extrabold uppercase tracking-wider text-[hsl(var(--e))]"
        >
          {entity}
        </span>
        <h2 id={titleId} className="font-quicksand text-xl font-bold text-foreground">
          {title}
        </h2>
        <span
          data-testid="entity-count"
          className="font-mono text-xs tabular-nums text-muted-foreground"
        >
          {count}
        </span>
        {viewAllHref && (
          <Link
            href={viewAllHref}
            aria-label={`Vedi tutti i ${title}, vai a ${viewAllHref}`}
            className="ml-auto font-quicksand text-sm font-bold text-[hsl(var(--e))] hover:underline"
          >
            Vedi tutti →
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}
