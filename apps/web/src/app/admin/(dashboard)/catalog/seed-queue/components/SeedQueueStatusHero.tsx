'use client';

import { AlertCircle } from 'lucide-react';

import { useCatalogSeeds } from '../hooks/use-catalog-seeds';

/**
 * Issue #1903 M8.3 — KPI hero for the catalog seed queue.
 *
 * Adapted from `SyncStatusHero` (#1835). Instead of provider config, this hero
 * shows aggregate counts per status (Pending / Fetched / Approved / Rejected).
 * Each count drives a single `useCatalogSeeds` query with `take=0` so the BE
 * can return just the total without paying the projection cost — this mirrors
 * the pattern used by AdminCacheStats etc.
 */
const STATUSES = ['Pending', 'Fetched', 'Approved', 'Rejected'] as const;

const TONE: Record<(typeof STATUSES)[number], { ring: string; text: string; label: string }> = {
  Pending: { ring: 'ring-entity-toolkit/30', text: 'text-entity-toolkit', label: 'Pending' },
  Fetched: { ring: 'ring-entity-game/30', text: 'text-entity-game', label: 'Fetched' },
  Approved: {
    ring: 'ring-entity-collection/30',
    text: 'text-entity-collection',
    label: 'Approved',
  },
  Rejected: { ring: 'ring-entity-event/30', text: 'text-entity-event', label: 'Rejected' },
};

interface StatusCountCardProps {
  status: (typeof STATUSES)[number];
}

function StatusCountCard({ status }: StatusCountCardProps) {
  const { data, isError } = useCatalogSeeds({ status, skip: 0, take: 1 });
  const tone = TONE[status];
  const value = data?.total ?? (isError ? '—' : '…');
  return (
    <div
      className={`rounded-lg border border-border bg-card px-4 py-3 ring-1 ${tone.ring}`}
      data-testid={`seed-status-card-${status.toLowerCase()}`}
    >
      <p className={`font-mono text-[10px] uppercase tracking-[0.04em] ${tone.text}`}>
        {tone.label}
      </p>
      <p className="mt-0.5 font-quicksand text-2xl font-extrabold text-foreground">{value}</p>
    </div>
  );
}

export function SeedQueueStatusHero() {
  const { isError, error, refetch } = useCatalogSeeds({ skip: 0, take: 1 });

  if (isError) {
    return (
      <div
        role="alert"
        className="flex items-center gap-3 rounded-xl border border-entity-event/30 bg-entity-event/[0.04] px-5 py-4"
      >
        <AlertCircle className="h-5 w-5 shrink-0 text-entity-event" aria-hidden />
        <div className="flex-1">
          <h3 className="font-quicksand text-sm font-bold text-foreground">
            Catalog seed queue unavailable
          </h3>
          <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
            {error instanceof Error ? error.message : 'Errore di rete'}
          </p>
        </div>
        <button
          type="button"
          onClick={() => refetch()}
          className="rounded-md border border-border bg-background px-3 py-1 text-xs font-medium hover:bg-muted"
        >
          Riprova
        </button>
      </div>
    );
  }

  return (
    <section
      aria-label="Catalog seed queue status"
      className="rounded-xl border border-entity-toolkit/25 bg-gradient-to-br from-entity-toolkit/[0.14] to-entity-game/[0.08] px-6 py-5"
    >
      <header className="mb-3 flex items-center gap-2.5">
        <h2 className="font-quicksand text-[22px] font-extrabold leading-none text-foreground">
          🌱 Catalog seed queue
        </h2>
        <p className="ml-2 text-sm text-muted-foreground">
          Admin-only fetch + approve pipeline (Wikidata primary · BGG fallback)
        </p>
      </header>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        {STATUSES.map(s => (
          <StatusCountCard key={s} status={s} />
        ))}
      </div>
    </section>
  );
}
