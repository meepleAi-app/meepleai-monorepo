'use client';

import Link from 'next/link';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface EntityZoneProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  viewAllHref?: string;
  children: React.ReactNode;
}

export function EntityZone({ entity, title, count, viewAllHref, children }: EntityZoneProps) {
  const dotColor = entityHsl(entity);

  return (
    <section className="space-y-3">
      {/* Zone header */}
      <div className="flex items-center gap-2.5">
        <div
          data-testid="entity-dot"
          className="h-2.5 w-2.5 rounded-full"
          style={{ backgroundColor: dotColor }}
        />
        <h3 className="font-quicksand text-[13px] font-bold uppercase tracking-[0.08em] text-muted-foreground">
          {title}
        </h3>
        <span className="text-[11px] tabular-nums text-muted-foreground/60">{count}</span>
        {viewAllHref && (
          <Link
            href={viewAllHref}
            className="ml-auto text-xs font-semibold text-[hsl(25,95%,45%)] hover:underline"
          >
            Vedi tutti →
          </Link>
        )}
      </div>

      {/* Zone content */}
      {children}
    </section>
  );
}
