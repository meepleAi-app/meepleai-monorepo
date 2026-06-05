'use client';

import { useState } from 'react';

import { Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

import { useEnqueueSeed, useWikidataSearch } from '../hooks/use-catalog-seeds';

import type { FieldProvenance } from '../lib/catalog-seed-types';

/**
 * Issue #1903 M8.4 — Wikidata SPARQL search panel.
 *
 * Pre-canned SPARQL query is issued server-side via the
 * `/wikidata-search` endpoint. The hit list shows the canonical fields
 * (title / year / designer) plus an "Add to queue" button per hit that
 * forwards the Wikidata QID to `EnqueueCatalogSeedCommand`.
 */
type RowField = { value: string; sourceUrl: string | null };

function readField(field: FieldProvenance | undefined): RowField | null {
  if (!field) return null;
  let value: string;
  if (typeof field.value === 'string') {
    value = field.value;
  } else if (Array.isArray(field.value)) {
    value = (field.value as unknown[]).map(v => String(v)).join(', ');
  } else if (field.value === null || field.value === undefined) {
    return null;
  } else {
    value = String(field.value);
  }
  return { value, sourceUrl: field.sourceUrl ?? null };
}

function extractQidFromUrl(url: string): string | null {
  const match = url.match(/Q\d+/);
  return match ? match[0] : null;
}

export function WikidataSearchForm() {
  const [term, setTerm] = useState('');
  const search = useWikidataSearch();
  const enqueue = useEnqueueSeed();

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (term.trim().length < 2) return;
    try {
      await search.mutateAsync(term.trim());
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Search failed');
    }
  };

  const handleAdd = async (qid: string) => {
    try {
      const result = await enqueue.mutateAsync({ wikidataQid: qid });
      if (result.isDuplicate) {
        toast.info(`Draft already exists for ${qid}`);
      } else {
        toast.success(`Seed enqueued: ${qid}`);
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Enqueue failed');
    }
  };

  const result = search.data;
  const title = result ? readField(result.fields.title) : null;
  const year = result ? readField(result.fields.yearPublished ?? result.fields.year) : null;
  const designer = result ? readField(result.fields.designer) : null;
  // Try to surface a Wikidata QID from any field's sourceUrl. Wikidata permanent
  // URIs always include the QID, so this is robust across field shapes.
  const qid =
    result &&
    Object.values(result.fields)
      .map(f => extractQidFromUrl(f.sourceUrl))
      .find((q): q is string => q !== null);

  return (
    <section aria-label="Wikidata search" className="rounded-xl border border-border bg-card p-4">
      <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
        🔍 Search Wikidata
      </h3>
      <p className="mt-1 text-xs text-muted-foreground">
        CC0 public-domain data, free from BGG ToS constraints.
      </p>
      <form className="mt-3 flex items-center gap-2" onSubmit={handleSearch}>
        <input
          aria-label="Wikidata search term"
          value={term}
          onChange={e => setTerm(e.target.value)}
          placeholder="board game title…"
          className="flex-1 rounded border border-border bg-background px-2 py-1.5 text-sm"
        />
        <Button type="submit" disabled={term.trim().length < 2 || search.isPending}>
          {search.isPending ? (
            <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
          ) : (
            <Search className="mr-1 h-3.5 w-3.5" />
          )}
          Search
        </Button>
      </form>

      {search.isError && (
        <p className="mt-3 text-xs text-entity-event" role="alert">
          {search.error instanceof Error ? search.error.message : 'Search failed'}
        </p>
      )}

      {result && result.errorMessage && (
        <p className="mt-3 text-xs text-entity-event" role="alert">
          {result.errorMessage}
        </p>
      )}

      {result && !result.errorMessage && Object.keys(result.fields).length === 0 && (
        <p className="mt-3 text-xs text-muted-foreground">No results.</p>
      )}

      {result && !result.errorMessage && Object.keys(result.fields).length > 0 && (
        <div
          className="mt-3 rounded-md border border-border bg-background p-3"
          data-testid="wikidata-search-result"
        >
          <div className="grid grid-cols-[80px_1fr] gap-y-1 text-xs">
            {title && (
              <>
                <span className="font-mono text-muted-foreground">Title</span>
                <span className="text-foreground">{title.value}</span>
              </>
            )}
            {year && (
              <>
                <span className="font-mono text-muted-foreground">Year</span>
                <span className="text-foreground">{year.value}</span>
              </>
            )}
            {designer && (
              <>
                <span className="font-mono text-muted-foreground">Designer</span>
                <span className="text-foreground">{designer.value}</span>
              </>
            )}
            {qid && (
              <>
                <span className="font-mono text-muted-foreground">QID</span>
                <span className="font-mono text-foreground">{qid}</span>
              </>
            )}
          </div>
          {qid && (
            <Button
              size="sm"
              className="mt-3"
              onClick={() => handleAdd(qid)}
              disabled={enqueue.isPending}
            >
              {enqueue.isPending ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : null}
              Add {qid} to queue
            </Button>
          )}
        </div>
      )}
    </section>
  );
}
