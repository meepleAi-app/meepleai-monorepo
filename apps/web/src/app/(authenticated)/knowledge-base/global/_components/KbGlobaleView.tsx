/**
 * KbGlobaleView.tsx
 * Issue #1482 Task 6 — Client orchestrator for /knowledge-base/global
 *
 * URL SSOT: reads ?q= and ?mode= from search params; pushes updated URL on
 * HeroSearch submit or clear. Branches on `q.trim().length > 0`:
 *   - Home branch (q empty)  → <KbHomeDesktop> (recent docs from useUserKbDocs)
 *   - Results branch (q set) → <KbSearchResultsDesktop> (useGlobalKbSearch)
 *
 * HeroSearch is always visible at the top of the page regardless of branch.
 *
 * No double-fetch design:
 *   - useUserKbDocs is always called (cached 5min, cheap, feeds home branch).
 *   - useGlobalKbSearch is always called but gated via `enabled: !isHomeBranch`
 *     (the hook's internal gate `query.length >= 2` is a second safety net).
 *
 * Labels: in-file constants for v1. Task 8 extracts them to i18n catalog
 * under `pages.kbGlobale.*`.
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see Task 2: HeroSearch, Task 4: KbHomeDesktop, Task 5: KbSearchResultsDesktop
 */

'use client';

import { type JSX, useCallback, useMemo } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { HeroSearch } from '@/components/features/kb-globale/HeroSearch';
import { KbHomeDesktop } from '@/components/features/kb-globale/KbHomeDesktop';
import { KbSearchResultsDesktop } from '@/components/features/kb-globale/KbSearchResultsDesktop';
import { useGlobalKbSearch } from '@/hooks/queries/useGlobalKbSearch';
import { useUserKbDocs } from '@/hooks/queries/useUserKbDocs';
import type { SearchMode } from '@/lib/api/schemas/kb-globale.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

// ---------------------------------------------------------------------------
// In-file label constants (v1 — i18n extraction deferred to Task 8)
// ---------------------------------------------------------------------------

const LABELS = {
  hero: {
    placeholder: 'Cerca nella knowledge base…',
    submit: 'Cerca',
    clear: 'Cancella',
    modeLabel: 'Modalità di ricerca',
    modeOptions: {
      Semantic: 'Semantica',
    } as Record<SearchMode, string>,
  },
  home: {
    heading: 'Documenti recenti',
    empty: {
      title: 'Nessun documento ancora',
      description: 'Carica un PDF dalla libreria per iniziare.',
      cta: 'Vai alla libreria',
    },
    errorTitle: 'Impossibile caricare i documenti',
    errorDescription: 'Si è verificato un errore. Riprova tra qualche istante.',
    retry: 'Riprova',
    docCardAriaLabel: (doc: KbDoc) => `${doc.fileName}${doc.gameName ? ` — ${doc.gameName}` : ''}`,
  },
  results: {
    resultsCount: (n: number, q: string) => `${n} risultat${n === 1 ? 'o' : 'i'} per «${q}»`,
    loadMore: 'Carica altri',
    loadingMore: 'Caricamento…',
    empty: {
      title: 'Nessun risultato',
      description: "Prova con termini diversi o controlla l'ortografia.",
    },
    errorTitle: 'Errore nella ricerca',
    errorDescription: 'La ricerca non è riuscita. Riprova tra qualche istante.',
    retry: 'Riprova',
    resultAriaLabel: (r: { docTitle: string; gameName: string }) => `${r.docTitle} — ${r.gameName}`,
    pageLabel: (page: number) => `Pagina ${page}`,
  },
} as const;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function KbGlobaleView(): JSX.Element {
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── URL state (SSOT) ───────────────────────────────────────────────────
  const q = searchParams.get('q')?.trim() ?? '';
  const modeRaw = searchParams.get('mode');
  // SearchMode is currently only 'Semantic'. Validate strictly so future
  // enum extensions are safe.
  const mode: SearchMode | undefined = modeRaw === 'Semantic' ? 'Semantic' : undefined;

  const isHomeBranch = q.length === 0;

  // ── Hooks (called unconditionally per React rules) ─────────────────────
  // useUserKbDocs: always consumed (home branch data + cached warmup).
  const recent = useUserKbDocs();

  // useGlobalKbSearch: always called but disabled on home branch.
  const search = useGlobalKbSearch({
    query: q,
    mode,
    enabled: !isHomeBranch,
  });

  // ── URL update helpers ─────────────────────────────────────────────────
  const pushUrl = useCallback(
    (newQ: string, newMode: SearchMode) => {
      const params = new URLSearchParams();
      if (newQ) params.set('q', newQ);
      // 'Semantic' is the default — omit from URL to keep it clean.
      if (newMode !== 'Semantic') params.set('mode', newMode);
      const qs = params.toString();
      router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
    },
    [router]
  );

  const clearUrl = useCallback(() => {
    router.push('/knowledge-base/global');
  }, [router]);

  // ── Derived values ─────────────────────────────────────────────────────
  const recentDocs: readonly KbDoc[] = useMemo(
    () => recent.data?.items ?? [],
    [recent.data?.items]
  );

  // ── Retry handlers ─────────────────────────────────────────────────────
  const handleHomeRetry = useCallback(() => {
    void recent.refetch?.();
  }, [recent]);

  const handleResultsRetry = useCallback(() => {
    // The hook's internal state resets when the queryKey changes.
    // Re-pushing the same URL triggers a fresh render + re-enable.
    if (q) {
      const params = new URLSearchParams();
      params.set('q', q);
      if (mode && mode !== 'Semantic') params.set('mode', mode);
      router.push(`/knowledge-base/global?${params.toString()}`);
    }
  }, [q, mode, router]);

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col gap-6 w-full">
      <HeroSearch
        initialQuery={q}
        initialMode={mode ?? 'Semantic'}
        onSubmit={pushUrl}
        onClear={clearUrl}
        labels={LABELS.hero}
      />

      {isHomeBranch ? (
        <KbHomeDesktop
          recentDocs={recentDocs}
          isLoading={recent.isLoading}
          error={(recent.error as Error | null) ?? null}
          labels={LABELS.home}
          onRetry={handleHomeRetry}
        />
      ) : (
        <KbSearchResultsDesktop
          query={q}
          results={search.results}
          hasMore={search.hasMore}
          isLoading={search.isLoading}
          isFetchingNextPage={search.isFetchingNextPage}
          error={search.error}
          onLoadMore={search.fetchNextPage}
          labels={LABELS.results}
          onRetry={handleResultsRetry}
        />
      )}
    </div>
  );
}
