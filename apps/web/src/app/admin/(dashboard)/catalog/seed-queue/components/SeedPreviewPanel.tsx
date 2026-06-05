'use client';

import { useState } from 'react';

import { useCatalogSeed } from '../hooks/use-catalog-seeds';

import type { CatalogSeedDraftDto } from '../lib/catalog-seed-types';

/**
 * Issue #1903 M8.4 — expanded preview of a fetched catalog seed.
 *
 * Hits `GET /api/v1/admin/catalog/seeds/{id}` on demand to fetch the full draft.
 * Provenance fields are not yet on the public DTO (only summary cols), so we
 * surface what's available + an "Open raw payload" affordance referring to BE
 * §4.7 (detail endpoint will be extended in a follow-up). For now the panel
 * provides a reliable read of the metadata that is exposed.
 */
export interface SeedPreviewPanelProps {
  draft: CatalogSeedDraftDto;
}

function StatusPill({ status }: { status: string }) {
  const tone =
    status === 'Approved'
      ? 'bg-entity-collection/[0.12] text-entity-collection'
      : status === 'Fetched'
        ? 'bg-entity-game/[0.12] text-entity-game'
        : status === 'Pending'
          ? 'bg-entity-toolkit/[0.12] text-entity-toolkit'
          : status === 'FetchFailed' || status === 'Rejected'
            ? 'bg-entity-event/[0.12] text-entity-event'
            : 'bg-muted text-muted-foreground';
  return (
    <span
      aria-label={`Status: ${status}`}
      className={`inline-flex items-center rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.04em] ${tone}`}
    >
      {status}
    </span>
  );
}

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('it-IT', { hour12: false });
  } catch {
    return iso;
  }
}

export function SeedPreviewPanel({ draft }: SeedPreviewPanelProps) {
  const [showRaw, setShowRaw] = useState(false);
  // Always re-fetch the detail when expanded so we get any provenance the BE
  // exposes (M4.7) even if the list payload predates it.
  const { data: detail } = useCatalogSeed(draft.id);
  const enriched = detail ?? draft;

  return (
    <div
      className="space-y-3 rounded-md border border-border bg-card p-3"
      data-testid={`seed-preview-${draft.id}`}
    >
      <div className="grid grid-cols-[100px_1fr] gap-y-1 text-xs">
        <span className="font-mono text-muted-foreground">Status</span>
        <span>
          <StatusPill status={enriched.status} />
        </span>
        <span className="font-mono text-muted-foreground">BGG ID</span>
        <span className="font-mono text-foreground">
          {enriched.bggId ?? <span className="text-muted-foreground">—</span>}
        </span>
        <span className="font-mono text-muted-foreground">Wikidata QID</span>
        <span className="font-mono text-foreground">
          {enriched.wikidataQid ?? <span className="text-muted-foreground">—</span>}
        </span>
        <span className="font-mono text-muted-foreground">Search term</span>
        <span className="text-foreground">
          {enriched.searchTermInput ?? <span className="text-muted-foreground">—</span>}
        </span>
        <span className="font-mono text-muted-foreground">Created</span>
        <span className="text-foreground">{formatDate(enriched.createdAt)}</span>
        <span className="font-mono text-muted-foreground">Fetched</span>
        <span className="text-foreground">{formatDate(enriched.fetchedAt)}</span>
        <span className="font-mono text-muted-foreground">Approved</span>
        <span className="text-foreground">{formatDate(enriched.approvedAt)}</span>
        {enriched.resultingSharedGameId && (
          <>
            <span className="font-mono text-muted-foreground">SharedGameId</span>
            <span className="font-mono text-foreground">{enriched.resultingSharedGameId}</span>
          </>
        )}
      </div>

      {enriched.status === 'FetchFailed' && enriched.errorMessage && (
        <div className="rounded border-l-4 border-entity-event bg-entity-event/[0.04] px-3 py-2">
          <p className="font-mono text-[11px] font-bold text-entity-event">Fetch error</p>
          <p className="mt-1 font-mono text-[11px] text-muted-foreground">
            {enriched.errorMessage}
          </p>
        </div>
      )}

      <button
        type="button"
        onClick={() => setShowRaw(s => !s)}
        className="font-mono text-[10px] uppercase tracking-[0.04em] text-entity-toolkit underline"
      >
        {showRaw ? 'Hide raw JSON' : 'View raw DTO'}
      </button>
      {showRaw && (
        <pre className="overflow-x-auto rounded bg-muted/40 p-2 font-mono text-[10px] text-foreground">
          {JSON.stringify(enriched, null, 2)}
        </pre>
      )}
    </div>
  );
}
