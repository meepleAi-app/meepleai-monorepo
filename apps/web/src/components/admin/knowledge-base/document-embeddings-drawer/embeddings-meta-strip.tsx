import type { JSX } from 'react';

import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

/**
 * Issue #1674: meta-strip 4 KPI cards (Model · Dim · Total chunks · Indexed at).
 * Handles loading skeleton + 404 not-indexed empty state + error banner.
 */

export type EmbeddingsMetaState =
  | { status: 'loading' }
  | { status: 'success'; data: DocumentEmbeddingsMetaDto }
  | { status: 'not-indexed' }
  | { status: 'error'; message: string };

export interface EmbeddingsMetaStripProps {
  state: EmbeddingsMetaState;
}

export function EmbeddingsMetaStrip({ state }: EmbeddingsMetaStripProps): JSX.Element {
  if (state.status === 'loading') {
    return (
      <div
        className="grid grid-cols-4 gap-3"
        role="status"
        aria-label="Caricamento metadati embeddings"
      >
        {[0, 1, 2, 3].map(i => (
          <div
            key={i}
            data-testid="meta-skeleton"
            className="h-[88px] animate-pulse rounded-lg border border-border bg-muted"
          />
        ))}
      </div>
    );
  }

  if (state.status === 'not-indexed') {
    return (
      <div className="rounded-lg border border-border bg-muted p-6 text-center">
        <p className="text-sm font-semibold text-foreground">Documento non indicizzato</p>
        <p className="mt-1 text-xs text-muted-foreground">
          Esegui re-index dal pannello principale per generare gli embeddings.
        </p>
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-foreground">
        Impossibile caricare metadati embeddings. {state.message}
      </div>
    );
  }

  const { data } = state;
  const indexedLabel = formatIndexedAt(data.indexedAt);

  return (
    <div className="grid grid-cols-4 gap-3">
      <KpiCard label="Model" value={data.model} />
      <KpiCard label="Dimensions" value={String(data.dimensions)} unit="d" />
      <KpiCard label="Total chunks" value={String(data.totalChunks)} />
      <KpiCard label="Indexed at" value={indexedLabel} />
    </div>
  );
}

function formatIndexedAt(value: string | null): string {
  if (value === null) return '—';
  try {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('it-IT', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'UTC',
    }).format(date);
  } catch {
    return '—';
  }
}

function KpiCard({
  label,
  value,
  unit,
}: {
  label: string;
  value: string;
  unit?: string;
}): JSX.Element {
  return (
    <div className="flex min-h-[88px] flex-col gap-1 rounded-lg border border-border border-l-[3px] border-l-entity-kb bg-card p-3">
      <span className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
        {label}
      </span>
      <span className="break-words text-xl font-extrabold leading-tight text-foreground">
        {value}
        {unit ? (
          <span className="ml-0.5 text-xs font-bold text-muted-foreground">{unit}</span>
        ) : null}
      </span>
    </div>
  );
}
