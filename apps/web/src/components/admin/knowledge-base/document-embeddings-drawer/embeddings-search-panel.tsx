'use client';

import { useState, type FormEvent, type JSX } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useSearchDocumentChunks } from '@/hooks/admin/use-search-document-chunks';

import { EmbeddingsResultRow, type ScoredChunkDto } from './embeddings-result-row';

/**
 * Issue #1674: semantic search panel inside DocumentEmbeddingsDrawer.
 *
 * Riusa endpoint `POST /admin/kb/docs/{docId}/chunks/search` da FU-4 #1653.
 * Hook FE creato in G.0 (#1674 spin-out, FU-4 ha shipped solo BE).
 */

export interface EmbeddingsSearchPanelProps {
  docId: string;
}

const MAX_QUERY_LENGTH = 1000;
const LIMIT_OPTIONS = [5, 10, 20] as const;

export function EmbeddingsSearchPanel({ docId }: EmbeddingsSearchPanelProps): JSX.Element {
  const [query, setQuery] = useState('');
  const [limit, setLimit] = useState<number>(10);
  const search = useSearchDocumentChunks(docId);

  const queryTooLong = query.length > MAX_QUERY_LENGTH;
  const canSubmit = query.trim().length > 0 && !queryTooLong;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    search.mutate({ query: query.trim(), topK: limit });
  };

  const results: ScoredChunkDto[] = (search.data?.results ?? []) as ScoredChunkDto[];
  const hasResults = search.isSuccess && results.length > 0;
  const noResults = search.isSuccess && results.length === 0;
  const serverError =
    search.isSuccess && search.data?.errorMessage !== null ? search.data?.errorMessage : null;

  return (
    <section className="mt-6">
      <h3 className="mb-3 font-display text-sm font-extrabold">🔬 Ricerca semantica</h3>

      <form onSubmit={onSubmit} className="grid grid-cols-[1fr_120px_auto] gap-2">
        <input
          type="text"
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Cerca nei chunk del documento…"
          aria-label="Query semantica"
          aria-invalid={queryTooLong || undefined}
          aria-describedby={queryTooLong ? 'embeddings-query-too-long' : undefined}
          className="rounded-md border border-border bg-card px-3 py-1.5 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
        <select
          value={limit}
          onChange={e => setLimit(Number.parseInt(e.target.value, 10))}
          aria-label="Limit"
          className="rounded-md border border-border bg-card px-3 py-1.5 text-sm"
        >
          {LIMIT_OPTIONS.map(opt => (
            <option key={opt} value={opt}>
              limit {opt}
            </option>
          ))}
        </select>
        <Button type="submit" disabled={!canSubmit || search.isPending}>
          {search.isPending ? 'Cerca…' : 'Cerca'}
        </Button>
      </form>

      {queryTooLong ? (
        <p id="embeddings-query-too-long" className="mt-1 text-xs text-destructive">
          Query troppo lunga (max {MAX_QUERY_LENGTH} caratteri)
        </p>
      ) : null}

      <div className="mt-4 rounded-md border border-border">
        {search.isPending ? (
          <div className="space-y-1 p-2" data-testid="search-skeleton">
            {[0, 1, 2, 3, 4].map(i => (
              <div key={i} className="h-8 animate-pulse rounded bg-muted" />
            ))}
          </div>
        ) : search.isError ? (
          <p className="p-4 text-sm text-destructive">Errore ricerca: {search.error.message}</p>
        ) : serverError ? (
          <p className="p-4 text-sm text-destructive">{serverError}</p>
        ) : hasResults ? (
          <>
            <p
              className="border-b border-border px-3 py-2 text-xs text-muted-foreground"
              aria-live="polite"
            >
              {results.length} risultati trovati
            </p>
            {results.map(chunk => (
              <EmbeddingsResultRow key={chunk.chunkIndex} chunk={chunk} />
            ))}
          </>
        ) : noResults ? (
          <p className="p-4 text-sm text-muted-foreground" role="status">
            Nessun chunk corrisponde alla query.
          </p>
        ) : (
          <p className="p-4 text-sm text-muted-foreground">
            Digita una query e clicca Cerca per iniziare.
          </p>
        )}
      </div>
    </section>
  );
}
