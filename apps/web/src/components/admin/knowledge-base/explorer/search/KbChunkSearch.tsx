/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber/emerald/rose score badge palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import { useState } from 'react';

import { toast } from 'sonner';

import { useDocChunkSearch } from '@/hooks/queries/useKbDocActions';
import type { DocChunkSearchHit } from '@/lib/api/clients/pdfClient';

export interface KbChunkSearchProps {
  /** Document UUID */
  readonly docId: string;
  /** Total embedded chunks — if 0, the search input is disabled */
  readonly chunkCount: number;
}

/** Shared focus-visible ring classes applied to every bare button / input (a11y). */
const FOCUS_RING =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2';

/** Score tiers → Tailwind colour utilities (hue-based, no neutral hardcoded names). */
function scoreBadgeClass(score: number): string {
  if (score >= 0.7)
    return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30';
  if (score >= 0.5) return 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30';
  return 'bg-muted text-muted-foreground border-border';
}

type SearchState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'results'; hits: DocChunkSearchHit[] }
  | { kind: 'empty' }
  | { kind: 'error'; message: string };

const THRESHOLD_OPTIONS = [
  { label: 'Score ≥ 0.9', value: 0.9 },
  { label: 'Score ≥ 0.7', value: 0.7 },
  { label: 'Score ≥ 0.5', value: 0.5 },
  { label: 'Score ≥ 0.3', value: 0.3 },
  { label: 'Tutti i risultati', value: 0 },
];

/**
 * KbChunkSearch — in-panel semantic chunk similarity-search.
 *
 * Renders a search input + score-threshold selector.
 * On submit calls useDocChunkSearch.mutateAsync and displays results sorted
 * by score desc. Threshold is applied client-side so changing it does NOT
 * trigger a new network request.
 *
 * Issue #1653 F3-FU-4 Task 9.
 */
export function KbChunkSearch({ docId, chunkCount }: KbChunkSearchProps) {
  const [query, setQuery] = useState('');
  const [threshold, setThreshold] = useState(0.7);
  const [state, setState] = useState<SearchState>({ kind: 'idle' });
  // Raw results kept separately so threshold changes don't require a refetch
  const [rawHits, setRawHits] = useState<DocChunkSearchHit[]>([]);

  const { mutateAsync, isPending } = useDocChunkSearch(docId);

  const isDisabled = chunkCount === 0;

  /** Client-side threshold filter + stable desc sort */
  const visibleHits = rawHits.filter(h => h.score >= threshold).sort((a, b) => b.score - a.score);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = query.trim();
    if (!trimmed) return;

    setState({ kind: 'loading' });
    try {
      const result = await mutateAsync({ query: trimmed, minScore: threshold });

      if (result.errorMessage) {
        toast.error(`Ricerca fallita: ${result.errorMessage}`);
        setState({ kind: 'error', message: result.errorMessage });
        setRawHits([]);
        return;
      }

      const sorted = [...result.results].sort((a, b) => b.score - a.score);
      setRawHits(sorted);

      const above = sorted.filter(h => h.score >= threshold);
      setState(above.length > 0 ? { kind: 'results', hits: above } : { kind: 'empty' });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      toast.error(`Ricerca fallita: ${msg}`);
      setState({ kind: 'error', message: msg });
      setRawHits([]);
    }
  };

  // Re-derive visible hits when threshold changes (no network call)
  const handleThresholdChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const next = parseFloat(e.target.value);
    setThreshold(next);
    if (rawHits.length > 0) {
      const above = rawHits.filter(h => h.score >= next);
      setState(above.length > 0 ? { kind: 'results', hits: above } : { kind: 'empty' });
    }
  };

  return (
    <div className="mb-4">
      {/* ── Search bar ─────────────────────────────────────────────────────── */}
      <form onSubmit={e => void handleSubmit(e)} className="flex items-center gap-2 mb-3">
        {/* Input */}
        <div className="relative flex-1">
          <label htmlFor="kb-chunk-search-input" className="sr-only">
            Cerca nei chunk per similarità
          </label>
          <span
            aria-hidden="true"
            className="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground text-xs"
          >
            🔍
          </span>
          <input
            id="kb-chunk-search-input"
            type="search"
            role="searchbox"
            value={query}
            onChange={e => setQuery(e.target.value)}
            disabled={isDisabled || isPending}
            placeholder="Cerca in chunks… (similarity match)"
            aria-label="Cerca nei chunk per similarità"
            className={`w-full pl-7 pr-3 py-1.5 text-xs border border-border rounded-md bg-background text-foreground placeholder:text-muted-foreground disabled:opacity-50 disabled:cursor-not-allowed ${FOCUS_RING}`}
          />
        </div>

        {/* Threshold selector */}
        <label htmlFor="kb-chunk-search-threshold" className="sr-only">
          Soglia score
        </label>
        <select
          id="kb-chunk-search-threshold"
          aria-label="Soglia score"
          value={threshold}
          onChange={handleThresholdChange}
          disabled={isDisabled}
          className={`py-1.5 px-2 text-xs border border-border rounded-md bg-background text-foreground disabled:opacity-50 disabled:cursor-not-allowed ${FOCUS_RING}`}
        >
          {THRESHOLD_OPTIONS.map(opt => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>

        {/* Submit button */}
        <button
          type="submit"
          disabled={isDisabled || isPending || !query.trim()}
          className={`px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70 disabled:opacity-50 disabled:cursor-not-allowed ${FOCUS_RING}`}
        >
          Cerca
        </button>
      </form>

      {/* ── State output ────────────────────────────────────────────────────── */}

      {/* No-chunks hint */}
      {isDisabled && (
        <p data-testid="kb-chunk-search-no-chunks" className="text-xs text-muted-foreground italic">
          Documento senza chunk — la ricerca non è disponibile.
        </p>
      )}

      {/* Idle */}
      {!isDisabled && state.kind === 'idle' && (
        <p data-testid="kb-chunk-search-idle" className="text-xs text-muted-foreground italic">
          Digita una query e premi Invio per cercare tra i chunk del documento.
        </p>
      )}

      {/* Loading */}
      {state.kind === 'loading' || isPending ? (
        state.kind === 'loading' || isPending ? (
          <div
            data-testid="kb-chunk-search-loading"
            className="flex items-center gap-2 py-3 text-xs text-muted-foreground"
          >
            <span className="animate-spin" aria-hidden="true">
              ⟳
            </span>
            Ricerca in corso…
          </div>
        ) : null
      ) : null}

      {/* Error */}
      {state.kind === 'error' && !isPending && (
        <div
          data-testid="kb-chunk-search-error"
          className="rounded-md border border-rose-500/30 bg-rose-500/5 p-3 text-xs text-rose-700 dark:text-rose-300"
        >
          <span className="font-semibold">Errore: </span>
          {state.message}
        </div>
      )}

      {/* Empty */}
      {state.kind === 'empty' && !isPending && (
        <p
          data-testid="kb-chunk-search-empty"
          className="text-xs text-muted-foreground italic py-2"
        >
          Nessun risultato per la query corrente con la soglia impostata.
        </p>
      )}

      {/* Results */}
      {state.kind === 'results' && !isPending && visibleHits.length > 0 && (
        <ul
          data-testid="kb-chunk-search-results"
          className="divide-y divide-border/60 dark:divide-zinc-700/60"
        >
          {visibleHits.map(hit => (
            <li key={hit.chunkIndex} data-testid="kb-chunk-search-hit" className="py-2.5">
              <div className="flex items-center gap-2 text-[10.5px] font-mono text-muted-foreground mb-0.5 flex-wrap">
                <code>c-{hit.chunkIndex.toString().padStart(4, '0')}</code>
                {hit.pageNumber !== null && <span>p.{hit.pageNumber}</span>}
                {/* Score badge */}
                <span
                  className={`inline-flex items-center px-1.5 py-0.5 text-[10px] font-semibold rounded-full border ${scoreBadgeClass(hit.score)}`}
                >
                  {hit.score.toFixed(2)}
                </span>
              </div>
              <p className="text-[12.5px] text-foreground leading-snug line-clamp-2">
                {hit.snippet}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
